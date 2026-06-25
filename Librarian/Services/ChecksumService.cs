using Librarian.DB;
using Librarian.Metadata;
using Librarian.Model;
using Librarian.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Librarian.Services
{
    /// <summary>How aggressively content hashing runs. See plan.md Phase 4.</summary>
    public enum ChecksumMode
    {
        /// <summary>No hashing at all (default).</summary>
        Off,

        /// <summary>Lazy, staged hashing for duplicate detection: size → prefix hash → full hash, only
        /// escalating when files still collide. Most files are never fully read.</summary>
        Dedup,

        /// <summary>Always compute the full SHA-256 (a prefix hash can't catch mid-file bitrot), so the
        /// stored checksum can validate a file is intact.</summary>
        Integrity,
    }

    public class DuplicateSet
    {
        public string Hash { get; set; } = string.Empty;
        public long? Size { get; set; }
        public List<string> Paths { get; set; } = new();
    }

    public class ChecksumResult
    {
        public ChecksumMode Mode { get; set; }
        public int Scanned { get; set; }
        public int PrefixHashed { get; set; }
        public int FullHashed { get; set; }
        public int DuplicateSets { get; set; }
        public int DuplicateFiles { get; set; }
    }

    /// <summary>
    /// Content hashing (SHA-256) for integrity validation and duplicate detection. Runs as its own
    /// pass (not inline with metadata extraction — hashing is I/O-bound) and is change-gated: the prefix
    /// hash lives on <see cref="IndexedFile.PrefixHash"/> and is cleared when a file changes; the full
    /// hash is stored as the read-only "File attributes/Checksum" canonical attribute, so a normal
    /// incremental index leaves it intact and only a changed/force-reindexed file drops it for recompute.
    /// </summary>
    public class ChecksumService
    {
        /// <summary>Provider id stamped on the full-hash canonical attribute.</summary>
        public static readonly Guid ChecksumProvider = new("a1b2c3d4-5e6f-4708-9a1b-2c3d4e5f6a7b");

        private const int DefaultPrefixBytes = 64 * 1024;

        private readonly DatabaseContext db;
        private readonly MetadataFactory factory;
        private readonly FileService fileService;
        private readonly IConfiguration config;
        private readonly ILogger<ChecksumService> logger;

        public ChecksumService(DatabaseContext db,
                               MetadataFactory factory,
                               FileService fileService,
                               IConfiguration config,
                               ILogger<ChecksumService> logger)
        {
            this.db = db;
            this.factory = factory;
            this.fileService = fileService;
            this.config = config;
            this.logger = logger;
        }

        /// <summary>The configured mode (<c>Checksum:Mode</c>); Off when unset/unrecognized.</summary>
        public ChecksumMode Mode =>
            Enum.TryParse<ChecksumMode>(config["Checksum:Mode"], ignoreCase: true, out var m) ? m : ChecksumMode.Off;

        private int PrefixBytes =>
            int.TryParse(config["Checksum:PrefixBytes"], out var n) && n > 0 ? n : DefaultPrefixBytes;

        /// <summary>
        /// Runs the hashing pass. <paramref name="overrideMode"/> forces a mode regardless of config
        /// (e.g. an admin "validate now"); otherwise the configured <see cref="Mode"/> is used.
        /// </summary>
        public async Task<ChecksumResult> RunAsync(ChecksumMode? overrideMode = null)
        {
            var mode = overrideMode ?? Mode;
            var result = new ChecksumResult { Mode = mode };
            if (mode == ChecksumMode.Off)
                return result;

            // Files only (directories have no Size), and only ones currently on disk.
            var files = await db.IndexedFiles.Where(f => f.Exists && f.Size != null).ToListAsync();
            result.Scanned = files.Count;

            if (mode == ChecksumMode.Integrity)
                await RunIntegrityAsync(files, result);
            else
                await RunDedupAsync(files, result);

            var duplicates = await GetDuplicatesAsync();
            result.DuplicateSets = duplicates.Count;
            result.DuplicateFiles = duplicates.Sum(d => d.Paths.Count);
            return result;
        }

        /// <summary>Integrity: every file gets a full hash (compute the ones still missing one).</summary>
        private async Task RunIntegrityAsync(IReadOnlyList<IndexedFile> files, ChecksumResult result)
        {
            var alreadyHashed = await HashedFileIdsAsync();
            foreach (var file in files)
            {
                if (alreadyHashed.Contains(file.Id))
                    continue;
                if (await StoreFullHashAsync(file))
                    result.FullHashed++;
            }
            await db.SaveChangesAsync();
        }

        /// <summary>Dedup: escalate size → prefix → full, only hashing where files still collide.</summary>
        private async Task RunDedupAsync(IReadOnlyList<IndexedFile> files, ChecksumResult result)
        {
            var alreadyHashed = await HashedFileIdsAsync();

            foreach (var sizeGroup in files.GroupBy(f => f.Size))
            {
                var sized = sizeGroup.ToList();
                if (sized.Count < 2)
                    continue;   // unique on size alone — no read needed

                // Prefix hash separates same-size files cheaply.
                foreach (var file in sized.Where(f => f.PrefixHash == null))
                {
                    file.PrefixHash = await ComputePrefixHashAsync(file);
                    if (file.PrefixHash != null)
                        result.PrefixHashed++;
                }
                await db.SaveChangesAsync();

                foreach (var prefixGroup in sized.Where(f => f.PrefixHash != null)
                                                 .GroupBy(f => Convert.ToHexString(f.PrefixHash!)))
                {
                    var candidates = prefixGroup.ToList();
                    if (candidates.Count < 2)
                        continue;   // unique on size+prefix — almost certainly not a duplicate

                    // Confirm with the full hash.
                    foreach (var file in candidates)
                    {
                        if (alreadyHashed.Contains(file.Id))
                            continue;
                        if (await StoreFullHashAsync(file))
                            result.FullHashed++;
                    }
                    await db.SaveChangesAsync();
                }
            }
        }

        /// <summary>Duplicate sets: files sharing a full content hash (only files that have been fully
        /// hashed appear — exactly the size+prefix collisions the dedup pass confirmed).</summary>
        public async Task<List<DuplicateSet>> GetDuplicatesAsync()
        {
            int checksumDef = Model.MetadataAttributes.FileAttributes.Checksum;

            var rows = await (from a in db.TextAttributes
                              join f in db.IndexedFiles on a.FileId equals f.Id
                              where a.AttributeDefinitionId == checksumDef && a.SubResourceId == null && f.Exists
                              select new { a.Value, f.Path, f.Size }).ToListAsync();

            return rows.GroupBy(r => r.Value)
                       .Where(g => g.Count() > 1)
                       .Select(g => new DuplicateSet
                       {
                           Hash = g.Key,
                           Size = g.First().Size,
                           Paths = g.Select(x => x.Path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                       })
                       .OrderByDescending(s => s.Paths.Count)
                       .ThenByDescending(s => s.Size)
                       .ToList();
        }

        /// <summary>Ids of files that already carry a full-hash Checksum attribute.</summary>
        private async Task<HashSet<int>> HashedFileIdsAsync()
        {
            int checksumDef = Model.MetadataAttributes.FileAttributes.Checksum;
            var ids = await db.TextAttributes
                .Where(a => a.AttributeDefinitionId == checksumDef && a.SubResourceId == null && a.FileId != null)
                .Select(a => a.FileId!.Value)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        /// <summary>Computes and stores a file's full hash as its Checksum attribute (replacing any prior
        /// one). Returns false if the file could not be read.</summary>
        private async Task<bool> StoreFullHashAsync(IndexedFile file)
        {
            byte[]? hash = await TryHashAsync(() => HashFileAsync(fileService.Resolve(file.Path)), file.Path);
            if (hash == null)
                return false;

            int checksumDef = Model.MetadataAttributes.FileAttributes.Checksum;
            db.TextAttributes.RemoveRange(db.TextAttributes
                .Where(a => a.FileId == file.Id && a.SubResourceId == null && a.AttributeDefinitionId == checksumDef));

            var attribute = factory.Create(checksumDef, Convert.ToHexString(hash), ChecksumProvider, editable: false);
            attribute.File = file;
            db.TextAttributes.Add((TextAttribute)attribute);
            return true;
        }

        private async Task<byte[]?> ComputePrefixHashAsync(IndexedFile file) =>
            await TryHashAsync(() => HashPrefixAsync(fileService.Resolve(file.Path), PrefixBytes), file.Path);

        private async Task<byte[]?> TryHashAsync(Func<Task<byte[]>> hash, string path)
        {
            try
            {
                return await hash();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not hash '{path}'; skipping.", path);
                return null;
            }
        }

        /// <summary>SHA-256 of a whole file (streamed).</summary>
        public static async Task<byte[]> HashFileAsync(string path)
        {
            await using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return await sha.ComputeHashAsync(stream);
        }

        /// <summary>SHA-256 of a file's first <paramref name="prefixBytes"/> bytes (or the whole file if
        /// it is shorter).</summary>
        public static async Task<byte[]> HashPrefixAsync(string path, int prefixBytes)
        {
            await using var stream = File.OpenRead(path);
            byte[] buffer = new byte[prefixBytes];
            int read = 0;
            int n;
            while (read < buffer.Length && (n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read))) > 0)
                read += n;

            return SHA256.HashData(buffer.AsSpan(0, read));
        }
    }
}
