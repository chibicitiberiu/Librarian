using Librarian.DB;
using Librarian.Library;
using Librarian.Metadata;
using Librarian.Metadata.Archives;
using Librarian.Metadata.Normalization;
using Librarian.Metadata.Providers;
using Librarian.Metadata.Providers.Tika;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Services
{
    public class MetadataService
    {
        /// <summary>Name of the hidden, folder-level sidecar that stores user-authored metadata
        /// (and, later, Item/role facts). One per edited folder, co-located with the files it describes;
        /// it is the on-disk system of record — the DB is rebuildable from file contents + this sidecar.</summary>
        public const string SidecarFileName = ".librarian.meta";

        /// <summary>Provider id stamped on canonical rows that originate from a user edit (the sidecar),
        /// so they can be told apart from extracted/promoted values.</summary>
        public static readonly Guid SidecarProvider = new("b6e1f3a4-2c7d-4e58-9a0b-1f2e3d4c5b6a");

        private readonly Dictionary<Guid, IMetadataProvider> metadataProviders = new();
        private readonly IReadOnlyList<IRawMetadataProvider> rawProviders;
        private readonly MetadataNormalizer normalizer;
        private readonly MetadataFactory factory;
        private readonly ILogger logger;
        private readonly MetadataSerializer serializer;
        private readonly FileService fileService;
        private readonly DatabaseContext dbContext;
        private readonly SearchVectorService searchVectors;
        private readonly ProviderExecutor executor;
        private readonly ArchiveByteReader archiveBytes;

        /// <summary>Safety ceiling on archive entries materialized as virtual files (collection_plan.md
        /// §7.4): above this, the archive is left opaque (no expansion) and the skip is logged.</summary>
        private const int DefaultMaxExpandedEntries = 50_000;
        private readonly int maxExpandedEntries;

        /// <summary>Whether to temp-materialize archive entries and re-run path-based providers (ExifTool,
        /// meta-cli) on them (collection_plan.md §7.3). On by default; gate is per-entry by media type.</summary>
        private readonly bool reExtractEntries;

        public MetadataService(ILogger<MetadataService> logger,
                               IEnumerable<IMetadataProvider> providers,
                               IEnumerable<IRawMetadataProvider> rawProviders,
                               MetadataNormalizer normalizer,
                               MetadataFactory factory,
                               MetadataSerializer serializer,
                               FileService fileService,
                               DatabaseContext dbContext,
                               SearchVectorService searchVectors,
                               ProviderExecutor executor,
                               ArchiveByteReader archiveBytes,
                               Microsoft.Extensions.Configuration.IConfiguration config)
        {
            this.logger = logger;
            this.serializer = serializer;

            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);

            this.rawProviders = rawProviders.ToList();
            this.normalizer = normalizer;
            this.factory = factory;
            this.fileService = fileService;
            this.dbContext = dbContext;
            this.searchVectors = searchVectors;
            this.executor = executor;
            this.archiveBytes = archiveBytes;
            maxExpandedEntries = int.TryParse(config["Archive:MaxExpandedEntries"], out int max) && max > 0
                ? max : DefaultMaxExpandedEntries;
            reExtractEntries = !bool.TryParse(config["Archive:ReExtractEntries"], out bool re) || re;
        }

        #region Updating, collecting metadata

        /// <summary>
        /// Collects metadata for a file and stores it: canonical providers and the .meta
        /// sidecar are merged into the canonical tables, while raw providers populate the
        /// raw layer and are promoted to canonical attributes through the normalizer.
        /// </summary>
        public async Task UpdateMetadata(IndexedFile indexedFile)
        {
            // Idempotent rebuild: drop this file's previously-extracted canonical attributes before
            // re-adding, so re-indexing replaces rather than appends (RC-6). Association-promoted
            // attributes are owned by the association pass, so they are preserved.
            RemoveDerivedCanonical(indexedFile.Id);

            // Canonical providers (file attributes, media), run under the resilience policy so a
            // transient provider failure is retried and, if it persists, flags the file.
            bool incomplete = await StoreCanonicalProvidersAsync(indexedFile);
            await dbContext.SaveChangesAsync();

            // Raw providers (Tika, ExifTool): persist the raw layer and promote it to canonical.
            incomplete |= await UpdateRawMetadata(indexedFile);

            // User edits in the .meta sidecar are authoritative: replace the matching extracted/promoted
            // values so the DB reflects what's on disk (the system of record).
            await ApplySidecarOverrides(indexedFile);

            // Record whether extraction was cut short by a transient failure, so the file can be
            // targeted for a later re-index (POST /api/metadata/reindex-incomplete).
            indexedFile.ExtractionIncomplete = incomplete;
            await dbContext.SaveChangesAsync();

            // Refresh the FTS vectors for the content + text attributes just written.
            await searchVectors.UpdateFileVectorsAsync(indexedFile.Id);
        }

        /// <summary>Runs the canonical providers under the resilience policy, stores their attributes,
        /// and reports whether any provider's extraction failed transiently (the file is incomplete).</summary>
        private async Task<bool> StoreCanonicalProvidersAsync(IndexedFile file)
        {
            string filePath = fileService.Resolve(file.Path);
            bool incomplete = false;

            foreach (var provider in metadataProviders.Values)
            {
                var (collection, failed) = await executor.ExecuteAsync(
                    provider.ProviderId, provider.DisplayName, () => provider.GetMetadataAsync(filePath));
                incomplete |= failed;
                if (collection is null)
                    continue;

                foreach (var attribute in collection.Attributes)
                {
                    attribute.File = file;
                    if (attribute.SubResource != null)
                        attribute.SubResource.File = file;
                    StoreCanonical(attribute);
                }
            }

            return incomplete;
        }

        private async Task<bool> UpdateRawMetadata(IndexedFile indexedFile)
        {
            string filePath = fileService.Resolve(indexedFile.Path);
            string? content = null;
            bool incomplete = false;

            // Replace this file's raw layer.
            dbContext.RawMetadataAttributes.RemoveRange(
                dbContext.RawMetadataAttributes.Where(r => r.FileId == indexedFile.Id));

            // De-duplicates promoted attributes by (field, sub-resource, value): several raw keys can
            // map to the same canonical field+value (e.g. albumartist spelt three ways), and we want
            // one row per (field, value) on each sub-resource, not one per source key.
            var promotedKeys = new HashSet<(int Def, SubResource? Sub, string Value)>();

            // Files embedded in this one (archive entries), to be exploded into virtual files after the
            // provider loop (collection_plan.md §7). Carries the producing provider so the entry's raw
            // rows and promotions are stamped consistently.
            var embedded = new List<(Guid ProviderId, EmbeddedResource Resource)>();

            foreach (var provider in rawProviders)
            {
                string providerId = provider.ProviderId.ToString();

                // Replace the canonical attributes previously promoted from this provider.
                RemoveCanonicalForProvider(indexedFile.Id, providerId);

                var (result, failed) = await executor.ExecuteAsync(
                    provider.ProviderId, provider.DisplayName, () => provider.GetRawMetadataAsync(filePath));
                incomplete |= failed;
                if (result is null)
                    continue;

                if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(result.Content))
                    content = StripNulls(result.Content);

                foreach (var resource in result.Embedded)
                    embedded.Add((provider.ProviderId, resource));

                foreach (var item in result.Items)
                {
                    SubResource? subResource = item.SubResource;
                    if (subResource is not null)
                    {
                        subResource.File = indexedFile;
                        subResource = AddOrUpdate(subResource, dbContext.SubResources);
                    }

                    // Postgres text columns reject the NUL byte (0x00); extractors occasionally emit it
                    // from binary-ish content, which would otherwise abort the whole file's save.
                    string value = StripNulls(item.Value);

                    dbContext.RawMetadataAttributes.Add(new RawMetadataAttribute
                    {
                        File = indexedFile,
                        SubResource = subResource,
                        Namespace = item.Namespace,
                        Key = item.Key,
                        Value = value,
                        ProviderId = providerId
                    });

                    foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, value, provider.ProviderId, subResource))
                    {
                        if (!promotedKeys.Add((canonical.AttributeDefinitionId, subResource, CanonicalValueKey(canonical))))
                            continue;
                        canonical.File = indexedFile;
                        StoreNormalized(canonical);
                    }
                }
            }

            StoreContent(indexedFile, content);
            await dbContext.SaveChangesAsync();

            // Explode archive entries into their own virtual files (collection_plan.md §7).
            await ExpandArchiveAsync(indexedFile, embedded);

            return incomplete;
        }

        /// <summary>Stores (or clears) the file's extracted text content.</summary>
        private void StoreContent(IndexedFile indexedFile, string? content)
        {
            var contents = dbContext.IndexedFileContents.FirstOrDefault(c => c.FileId == indexedFile.Id);

            if (string.IsNullOrWhiteSpace(content))
            {
                if (contents is not null)
                    dbContext.IndexedFileContents.Remove(contents);
                return;
            }

            if (contents is null)
            {
                contents = new IndexedFileContents { FileId = indexedFile.Id };
                dbContext.IndexedFileContents.Add(contents);
            }

            contents.Content = content;
        }

        #region Archive expansion (virtual files for archive entries — collection_plan.md §7)

        /// <summary>
        /// Explodes an archive's entries into their own virtual <see cref="IndexedFile"/> rows (one per
        /// entry, keyed by <see cref="IndexedFile.ParentFileId"/> + <see cref="IndexedFile.InternalPath"/>),
        /// routing each entry's metadata/content to its own row rather than gluing it onto the archive.
        /// Idempotent: present entries are upserted, vanished entries (the archive changed) are dropped.
        /// Non-archives and over-ceiling archives are left opaque.
        /// </summary>
        private async Task ExpandArchiveAsync(IndexedFile archive, List<(Guid ProviderId, EmbeddedResource Resource)> embedded)
        {
            // Only real on-disk archives expand (nested archives are bounded in a later milestone).
            bool isArchive = archive.Source == FileSource.Filesystem && ArchiveTypes.IsArchive(archive.Path);

            var existingChildren = await dbContext.IndexedFiles
                .Where(f => f.ParentFileId == archive.Id)
                .ToListAsync();

            if (!isArchive || embedded.Count == 0 || embedded.Count > maxExpandedEntries)
            {
                if (isArchive && embedded.Count > maxExpandedEntries)
                    logger.LogWarning(
                        "Archive {path} has {count} entries, over the {max} ceiling; left opaque (entries not expanded).",
                        archive.Path, embedded.Count, maxExpandedEntries);

                if (existingChildren.Count > 0)
                {
                    dbContext.IndexedFiles.RemoveRange(existingChildren);
                    await dbContext.SaveChangesAsync();
                }
                return;
            }

            // One entry per internal path; merge if a provider repeats a path.
            var byPath = new Dictionary<string, (Guid ProviderId, EmbeddedResource Resource)>(StringComparer.Ordinal);
            foreach (var entry in embedded)
            {
                string key = NormalizeInternalPath(entry.Resource.InternalPath);
                if (key.Length == 0)
                    continue;
                if (byPath.TryGetValue(key, out var existing))
                    existing.Resource.Items.AddRange(entry.Resource.Items);
                else
                    byPath[key] = entry;
            }

            var childrenByPath = existingChildren
                .Where(c => c.InternalPath != null)
                .ToDictionary(c => c.InternalPath!, c => c, StringComparer.Ordinal);

            // Path-based providers (ExifTool, meta-cli) can only run on archives we have a byte-reader for.
            string archiveRealPath = fileService.Resolve(archive.Path);
            bool canReExtract = reExtractEntries && archiveBytes.Supports(archiveRealPath);

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (internalPath, entry) in byPath)
            {
                string displayPath = archive.Path + "!/" + internalPath;
                if (displayPath.Length > 4096)
                {
                    logger.LogWarning("Skipping archive entry with over-long path: {path}", displayPath);
                    continue;
                }
                present.Add(internalPath);

                if (!childrenByPath.TryGetValue(internalPath, out var child))
                {
                    child = new IndexedFile
                    {
                        Source = FileSource.ArchiveEntry,
                        ParentFileId = archive.Id,
                        InternalPath = internalPath,
                    };
                    dbContext.IndexedFiles.Add(child);
                }

                child.Path = displayPath;
                child.Exists = true;
                child.Size = entry.Resource.Size;
                child.Created = archive.Created;
                child.Modified = archive.Modified;
                child.NeedsUpdating = false;
                child.IndexLastUpdated = DateTimeOffset.UtcNow;
                // A changed archive re-extracts its entries; stale hashes are cleared (C2 recomputes).
                child.PrefixHash = null;
                await dbContext.SaveChangesAsync();   // assign child.Id before attaching its metadata

                await StoreEntryMetadataAsync(child, entry.ProviderId, entry.Resource);

                // Temp-materialize media entries so path-based providers can add deeper tags (§7.3).
                if (canReExtract && ShouldReExtract(internalPath))
                    await ReExtractEntryAsync(child, archiveRealPath, internalPath);

                // User overrides for this entry (sidecar beside the archive) win, and survive reindex (§8).
                await ApplySidecarOverrides(child);
            }

            // Entries that vanished from the archive → drop their virtual files (cascades to their metadata).
            var stale = existingChildren
                .Where(c => c.InternalPath != null && !present.Contains(c.InternalPath))
                .ToList();
            if (stale.Count > 0)
                dbContext.IndexedFiles.RemoveRange(stale);

            await dbContext.SaveChangesAsync();
        }

        /// <summary>Stores one archive entry's raw + promoted metadata and content on its virtual file row.</summary>
        private async Task StoreEntryMetadataAsync(IndexedFile child, Guid providerId, EmbeddedResource resource)
        {
            string providerIdStr = providerId.ToString();

            // Idempotent rebuild of this entry's metadata.
            dbContext.RawMetadataAttributes.RemoveRange(
                dbContext.RawMetadataAttributes.Where(r => r.FileId == child.Id));
            RemoveDerivedCanonical(child.Id);

            var promotedKeys = new HashSet<(int Def, SubResource? Sub, string Value)>();
            foreach (var item in resource.Items)
            {
                string value = StripNulls(item.Value);
                dbContext.RawMetadataAttributes.Add(new RawMetadataAttribute
                {
                    File = child,
                    Namespace = item.Namespace,
                    Key = item.Key,
                    Value = value,
                    ProviderId = providerIdStr,
                });

                foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, value, providerId, null))
                {
                    if (!promotedKeys.Add((canonical.AttributeDefinitionId, null, CanonicalValueKey(canonical))))
                        continue;
                    canonical.File = child;
                    StoreNormalized(canonical);
                }
            }

            StoreContent(child, string.IsNullOrWhiteSpace(resource.Content) ? null : StripNulls(resource.Content));
            await dbContext.SaveChangesAsync();
            await searchVectors.UpdateFileVectorsAsync(child.Id);
        }

        /// <summary>Normalizes a Tika embedded path to a stable locator (forward slashes, no leading slash).</summary>
        private static string NormalizeInternalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return path.Replace('\\', '/').TrimStart('/');
        }

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".avi", ".mov", ".webm", ".wmv", ".flv", ".m4v",
            ".mpg", ".mpeg", ".ts", ".m2ts", ".ogv", ".3gp",
        };

        /// <summary>Whether an archive entry is a media/image file worth re-running ExifTool/meta-cli on
        /// (Tika already covered documents/text). Type-gated so we don't spawn a subprocess per text entry.</summary>
        private static bool ShouldReExtract(string internalPath)
        {
            string name = Path.GetFileName(internalPath);
            return Sidecars.IsAudio(name) || Sidecars.IsImage(name)
                || VideoExtensions.Contains(Path.GetExtension(name));
        }

        /// <summary>
        /// Materializes one archive entry to a temp file and re-runs the path-based providers (ExifTool,
        /// meta-cli) on it, attaching their metadata to the entry's virtual file (collection_plan.md §7.3).
        /// Tika and the filesystem-facts provider are skipped: Tika already ran on the whole archive, and
        /// filesystem facts (mtime/size) belong to the entry, not the temp copy. Best-effort: a failure to
        /// read or extract an entry leaves it with the Tika-derived metadata it already has.
        /// </summary>
        private async Task ReExtractEntryAsync(IndexedFile child, string archiveRealPath, string internalPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "librarian-arc", Guid.NewGuid().ToString("N"));
            // Keep the entry's real filename so FilenameMetadataProvider (and content detectors) see it.
            string tempPath = Path.Combine(tempDir, Path.GetFileName(internalPath));
            try
            {
                Directory.CreateDirectory(tempDir);
                await using (var entryStream = archiveBytes.OpenEntry(archiveRealPath, internalPath))
                {
                    if (entryStream is null)
                        return;
                    await using var fs = File.Create(tempPath);
                    await entryStream.CopyToAsync(fs);
                }

                // Canonical providers except the filesystem-facts one.
                foreach (var provider in metadataProviders.Values.Where(p => p is not FileMetadataProvider))
                {
                    MetadataCollection? collection = null;
                    try { collection = await provider.GetMetadataAsync(tempPath); }
                    catch (Exception ex) { logger.LogTrace(ex, "Entry re-extract (canonical {p}) failed for {path}", provider.DisplayName, child.Path); }
                    if (collection is null)
                        continue;
                    foreach (var attribute in collection.Attributes)
                    {
                        attribute.File = child;
                        if (attribute.SubResource != null)
                            attribute.SubResource.File = child;
                        StoreCanonical(attribute);
                    }
                }

                // Raw providers except Tika (already ran on the whole archive).
                foreach (var provider in rawProviders.Where(p => p is not TikaProvider))
                {
                    RawMetadataResult? result = null;
                    try { result = await provider.GetRawMetadataAsync(tempPath); }
                    catch (Exception ex) { logger.LogTrace(ex, "Entry re-extract (raw {p}) failed for {path}", provider.DisplayName, child.Path); }
                    if (result is null)
                        continue;

                    string providerIdStr = provider.ProviderId.ToString();
                    var promotedKeys = new HashSet<(int Def, SubResource? Sub, string Value)>();
                    foreach (var item in result.Items)
                    {
                        SubResource? sub = item.SubResource;
                        if (sub is not null)
                        {
                            sub.File = child;
                            sub = AddOrUpdate(sub, dbContext.SubResources);
                        }
                        string value = StripNulls(item.Value);
                        dbContext.RawMetadataAttributes.Add(new RawMetadataAttribute
                        {
                            File = child,
                            SubResource = sub,
                            Namespace = item.Namespace,
                            Key = item.Key,
                            Value = value,
                            ProviderId = providerIdStr,
                        });

                        foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, value, provider.ProviderId, sub))
                        {
                            if (!promotedKeys.Add((canonical.AttributeDefinitionId, sub, CanonicalValueKey(canonical))))
                                continue;
                            canonical.File = child;
                            StoreNormalized(canonical);
                        }
                    }
                }

                await dbContext.SaveChangesAsync();
                await searchVectors.UpdateFileVectorsAsync(child.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to re-extract archive entry {path}", child.Path);
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
                catch (Exception ex) { logger.LogTrace(ex, "Could not delete temp dir {dir}", tempDir); }
            }
        }

        #endregion

        /// <summary>Removes a file's extracted canonical attributes (every provider) ahead of an
        /// idempotent rebuild, but keeps association-promoted ones — those are regenerated by the
        /// association pass, not the indexer, and must survive an incremental re-index.</summary>
        private void RemoveDerivedCanonical(int fileId)
        {
            string promo = ItemAssociationService.PromotionProvider.ToString();
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
        }

        private void RemoveCanonicalForProvider(int fileId, string providerId)
        {
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
        }

        /// <summary>Adds a canonical-provider attribute. The file's prior canonical rows were already
        /// cleared (see <see cref="RemoveDerivedCanonical"/>), so this is a plain, idempotent add.</summary>
        private void StoreCanonical(AttributeBase attribute)
        {
            if (attribute.SubResource is not null)
                attribute.SubResource = AddOrUpdate(attribute.SubResource, dbContext.SubResources);

            StoreNormalized(attribute);
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
        private static string? StripNulls(string? s) =>
            s is null || s.IndexOf('\0') < 0 ? s : s.Replace("\0", string.Empty);

        /// <summary>A stable string form of an attribute's value, for de-duplicating promotions.</summary>
        internal static string CanonicalValueKey(AttributeBase attribute) => attribute switch
        {
            TextAttribute a => a.Value,
            IntegerAttribute a => a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FloatAttribute a => a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateAttribute a => a.Value.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            BlobAttribute a => Convert.ToBase64String(a.Value),
            _ => string.Empty,
        };

        /// <summary>Adds a freshly-promoted attribute (the file's old ones were already removed).</summary>
        private void StoreNormalized(AttributeBase attribute)
        {
            switch (attribute)
            {
                case BlobAttribute a: dbContext.BlobAttributes.Add(a); break;
                case DateAttribute a: dbContext.DateAttributes.Add(a); break;
                case FloatAttribute a: dbContext.FloatAttributes.Add(a); break;
                case IntegerAttribute a: dbContext.IntegerAttributes.Add(a); break;
                case TextAttribute a: dbContext.TextAttributes.Add(a); break;
            }
        }

        private static SubResource AddOrUpdate(SubResource subResource, DbSet<SubResource> dbSet)
        {
            var existingSubResource = dbSet
                .Where(x => x.File == subResource.File
                                        && x.InternalId == subResource.InternalId
                                        && x.Kind == subResource.Kind
                                        && x.Name == subResource.Name)
                .FirstOrDefault();

            if (existingSubResource is not null)
                return existingSubResource;

            dbSet.Add(subResource);
            return subResource;
        }

        /// <summary>Canonical providers and the .meta sidecar for an indexed file (sets the file reference).</summary>
        private async IAsyncEnumerable<AttributeBase> CollectCanonicalAsync(IndexedFile file)
        {
            string filePath = fileService.Resolve(file.Path);

            await foreach (var attribute in CollectCanonicalAsync(filePath))
            {
                attribute.File = file;
                if (attribute.SubResource != null)
                    attribute.SubResource.File = file;
                yield return attribute;
            }
        }

        /// <summary>Canonical providers and the .meta sidecar (no raw providers, no persistence).</summary>
        private async IAsyncEnumerable<AttributeBase> CollectCanonicalAsync(string filePath)
        {
            foreach (var provider in metadataProviders.Values)
            {
                MetadataCollection? metadataCollection = null;
                try
                {
                    metadataCollection = await provider.GetMetadataAsync(filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collecting data from provider {providerName} failed", provider.DisplayName);
                }

                if (metadataCollection != null)
                {
                    foreach (var attribute in metadataCollection.Attributes)
                        yield return attribute;
                }
            }

            // The .meta sidecar is no longer merged here: it is applied as an authoritative override
            // (see ApplySidecarOverrides for persistence, CollectMetadataAsync for display), so a user
            // edit replaces — rather than duplicates — the matching extracted value.
        }

        /// <summary>Raw providers (Tika) promoted to canonical attributes, with their definition
        /// resolved for display. Does not persist.</summary>
        private async Task<List<AttributeBase>> CollectPromotedAsync(string filePath)
        {
            var list = new List<AttributeBase>();

            foreach (var provider in rawProviders)
            {
                RawMetadataResult? result = null;
                try
                {
                    result = await provider.GetRawMetadataAsync(filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collecting raw data from provider {providerName} failed", provider.DisplayName);
                }
                if (result is null)
                    continue;

                foreach (var item in result.Items)
                {
                    foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, item.Value, provider.ProviderId, item.SubResource))
                    {
                        canonical.AttributeDefinition ??= dbContext.AttributeDefinitions.Find(canonical.AttributeDefinitionId)!;
                        list.Add(canonical);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Collects all canonical metadata for display: canonical providers, the .meta
        /// sidecar, and raw providers promoted through the normalizer. Does not persist.
        /// </summary>
        public async IAsyncEnumerable<AttributeBase> CollectMetadataAsync(string filePath)
        {
            var result = new List<AttributeBase>();

            await foreach (var attribute in CollectCanonicalAsync(filePath))
                result.Add(attribute);

            result.AddRange(await CollectPromotedAsync(filePath));

            // Apply .meta sidecar overrides last: a user edit replaces the matching file-level
            // extracted/promoted value (same definition), rather than showing both.
            var overrides = (await LoadMetaFile(filePath)).ToList();
            if (overrides.Count > 0)
            {
                var overriddenDefs = overrides.Where(o => o.SubResource == null).Select(DefId).ToHashSet();
                result.RemoveAll(a => a.SubResource == null && overriddenDefs.Contains(DefId(a)));
                foreach (var o in overrides)
                {
                    o.AttributeDefinition ??= dbContext.AttributeDefinitions.Find(o.AttributeDefinitionId)!;
                    result.Add(o);
                }
            }

            foreach (var attribute in result)
                yield return attribute;
        }

        #endregion

        #region User edits / .meta write-back

        /// <summary>A single field's worth of user-submitted values from the edit form
        /// (one entry per (group, name); multiple values for a multi-valued field such as Tag).</summary>
        public record UserMetadataEdit(string? Group, string Name, IReadOnlyList<string> Values);

        /// <summary>
        /// Persists a user's metadata edits for one file to the folder's hidden <c>.librarian.meta</c>
        /// sidecar, then rebuilds the file's DB metadata so the change takes effect immediately.
        ///
        /// Only genuine overrides are written: a submitted value is kept only if it differs from what
        /// extraction itself reports (so the sidecar holds authorship, not a copy of the cache), and only
        /// for editable, non-filesystem definitions. The file's whole sidecar entry is rebuilt from the
        /// submission, so reverting a field back to its extracted value drops the override.
        /// </summary>
        public async Task SaveUserEditsAsync(IndexedFile file, IReadOnlyList<UserMetadataEdit> edits)
        {
            // Baseline = what extraction yields WITHOUT the sidecar, keyed by definition id. A filesystem
            // file is re-extracted from disk; an archive entry (no standalone disk path) uses its current
            // non-override DB attributes as the baseline.
            var baseline = file.Source == FileSource.ArchiveEntry
                ? await DbBaselineAsync(file.Id)
                : await ExtractionBaselineAsync(fileService.Resolve(file.Path));

            var overrides = BuildOverrides(edits, baseline);

            await WriteSidecarEntry(file, overrides);

            // Rebuild this file's DB metadata so the change takes effect immediately. A filesystem file is
            // rebuilt from disk + sidecar; an archive entry can't be re-extracted standalone, so its current
            // rows are kept and only the overridden definitions are replaced.
            if (file.Source == FileSource.ArchiveEntry)
            {
                await ApplySidecarOverrides(file);
                await searchVectors.UpdateFileVectorsAsync(file.Id);
            }
            else
            {
                await UpdateMetadata(file);
            }
        }

        /// <summary>What extraction yields for a filesystem file WITHOUT its sidecar (canonical + promoted).</summary>
        private async Task<Dictionary<int, HashSet<string>>> ExtractionBaselineAsync(string diskPath)
        {
            var baseline = new Dictionary<int, HashSet<string>>();
            await foreach (var attr in CollectCanonicalAsync(diskPath))
                AddBaseline(baseline, attr);
            foreach (var attr in await CollectPromotedAsync(diskPath))
                AddBaseline(baseline, attr);
            return baseline;
        }

        /// <summary>A file's current non-override canonical attributes as the edit baseline (for archive
        /// entries, which have no standalone disk path to re-extract).</summary>
        private async Task<Dictionary<int, HashSet<string>>> DbBaselineAsync(int fileId)
        {
            string sidecar = SidecarProvider.ToString();
            var baseline = new Dictionary<int, HashSet<string>>();

            foreach (var a in await dbContext.TextAttributes.AsNoTracking()
                .Where(a => a.FileId == fileId && a.SubResourceId == null && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.IntegerAttributes.AsNoTracking()
                .Where(a => a.FileId == fileId && a.SubResourceId == null && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.FloatAttributes.AsNoTracking()
                .Where(a => a.FileId == fileId && a.SubResourceId == null && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.DateAttributes.AsNoTracking()
                .Where(a => a.FileId == fileId && a.SubResourceId == null && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.BlobAttributes.AsNoTracking()
                .Where(a => a.FileId == fileId && a.SubResourceId == null && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);

            return baseline;
        }

        /// <summary>Turns submitted edit fields into the override attributes to persist: editable,
        /// non-filesystem definitions whose value actually differs from the baseline.</summary>
        private List<AttributeBase> BuildOverrides(IReadOnlyList<UserMetadataEdit> edits,
            Dictionary<int, HashSet<string>> baseline)
        {
            var overrides = new List<AttributeBase>();
            foreach (var edit in edits)
            {
                var def = dbContext.AttributeDefinitions
                    .FirstOrDefault(d => d.Group == edit.Group && d.Name == edit.Name);

                // Persist only user-authored descriptive metadata: skip unknown, read-only, and
                // filesystem facts (those are derived and rebuilt from disk, never authored here).
                if (def == null || def.IsReadOnly || def.Group == "File attributes")
                    continue;

                var values = edit.Values
                    .Select(v => v?.Trim() ?? string.Empty)
                    .Where(v => v.Length > 0)
                    .Distinct()
                    .ToList();

                // v1: an empty submission is treated as "no override" (a field can't yet be blanked to
                // override a non-empty extracted value).
                if (values.Count == 0)
                    continue;

                var built = new List<AttributeBase>();
                foreach (var value in values)
                {
                    try
                    {
                        built.Add(factory.Create(def, value, SidecarProvider, editable: true));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Skipping unparseable edit '{value}' for {group}/{name}", value, edit.Group, edit.Name);
                    }
                }
                if (built.Count == 0)
                    continue;

                // Unchanged from extraction → not an override.
                var builtKeys = built.Select(CanonicalValueKey).ToHashSet();
                if (baseline.TryGetValue(def.Id, out var baseKeys) && baseKeys.SetEquals(builtKeys))
                    continue;

                overrides.AddRange(built);
            }
            return overrides;
        }

        #region Collection write-back (collection_plan.md §8)

        /// <summary>The sidecar and key for a collection's overrides: the <c>.librarian.meta</c> in the
        /// collection's own folder, under a <c>&lt;collection&gt;</c> element keyed by its source path.</summary>
        private (string SidecarPath, string Key) CollectionSidecarLocation(Collection collection)
        {
            string folder = fileService.Resolve(collection.SourcePath!);
            return (Path.Combine(folder, SidecarFileName), collection.SourcePath!);
        }

        /// <summary>Persists a user's edits to a collection (e.g. correcting a Show title) into the
        /// collection folder's sidecar and re-applies them so the change takes effect and survives reindex.</summary>
        public async Task SaveCollectionEditsAsync(Collection collection, IReadOnlyList<UserMetadataEdit> edits)
        {
            if (string.IsNullOrEmpty(collection.SourcePath))
                return;   // no stable on-disk home to write the override to

            var baseline = await CollectionDbBaselineAsync(collection.Id);
            var overrides = BuildOverrides(edits, baseline);

            var (sidecar, key) = CollectionSidecarLocation(collection);
            var (files, collections) = await LoadSidecarMaps(sidecar);
            if (overrides.Count > 0)
                collections[key] = overrides;
            else
                collections.Remove(key);
            await WriteOrDeleteSidecar(sidecar, files, collections);

            await ApplyCollectionOverridesAsync(collection);
        }

        /// <summary>Applies a collection's sidecar overrides to its attributes (replacing the matching
        /// derived/promoted values). Called on save and during the association pass so they survive reindex.</summary>
        public async Task ApplyCollectionOverridesAsync(Collection collection)
        {
            if (string.IsNullOrEmpty(collection.SourcePath))
                return;

            var (sidecar, key) = CollectionSidecarLocation(collection);
            if (!File.Exists(sidecar))
                return;

            var map = await serializer.LoadFolderCollections(sidecar);
            if (!map.TryGetValue(key, out var overrides) || overrides.Count == 0)
                return;

            foreach (var defGroup in overrides.Where(a => a.SubResource == null).GroupBy(DefId))
            {
                RemoveCollectionCanonicalForDefinition(collection.Id, defGroup.Key);
                foreach (var attribute in defGroup)
                {
                    attribute.CollectionId = collection.Id;
                    attribute.File = null;
                    attribute.FileId = null;
                    attribute.ProviderId = SidecarProvider.ToString();
                    attribute.Editable = true;
                    StoreNormalized(attribute);
                }
            }

            await dbContext.SaveChangesAsync();
        }

        private async Task<Dictionary<int, HashSet<string>>> CollectionDbBaselineAsync(int collectionId)
        {
            string sidecar = SidecarProvider.ToString();
            var baseline = new Dictionary<int, HashSet<string>>();

            foreach (var a in await dbContext.TextAttributes.AsNoTracking()
                .Where(a => a.CollectionId == collectionId && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.IntegerAttributes.AsNoTracking()
                .Where(a => a.CollectionId == collectionId && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.FloatAttributes.AsNoTracking()
                .Where(a => a.CollectionId == collectionId && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.DateAttributes.AsNoTracking()
                .Where(a => a.CollectionId == collectionId && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);
            foreach (var a in await dbContext.BlobAttributes.AsNoTracking()
                .Where(a => a.CollectionId == collectionId && a.ProviderId != sidecar).ToListAsync())
                AddBaseline(baseline, a);

            return baseline;
        }

        private void RemoveCollectionCanonicalForDefinition(int collectionId, int definitionId)
        {
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId));
        }

        #endregion

        /// <summary>Replaces a single file's entry in its folder sidecar (filesystem file keyed by name, or
        /// archive entry keyed by its "archive!/internal" locator), preserving any sibling file and
        /// collection overrides; deletes the sidecar when nothing is left in it.</summary>
        private async Task WriteSidecarEntry(IndexedFile file, IReadOnlyList<AttributeBase> overrides)
        {
            var (sidecar, key) = SidecarLocation(file);

            var (files, collections) = await LoadSidecarMaps(sidecar);

            if (overrides.Count > 0)
                files[key] = overrides;
            else
                files.Remove(key);

            await WriteOrDeleteSidecar(sidecar, files, collections);
        }

        /// <summary>Loads a sidecar's file- and collection-keyed override maps (empty when absent).</summary>
        private async Task<(Dictionary<string, IReadOnlyList<AttributeBase>> Files,
                            Dictionary<string, IReadOnlyList<AttributeBase>> Collections)> LoadSidecarMaps(string sidecar)
        {
            var files = new Dictionary<string, IReadOnlyList<AttributeBase>>(StringComparer.OrdinalIgnoreCase);
            var collections = new Dictionary<string, IReadOnlyList<AttributeBase>>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(sidecar))
            {
                foreach (var e in await serializer.LoadFolder(sidecar))
                    files[e.Key] = e.Value;
                foreach (var e in await serializer.LoadFolderCollections(sidecar))
                    collections[e.Key] = e.Value;
            }
            return (files, collections);
        }

        private async Task WriteOrDeleteSidecar(string sidecar,
            Dictionary<string, IReadOnlyList<AttributeBase>> files,
            Dictionary<string, IReadOnlyList<AttributeBase>> collections)
        {
            if (files.Count == 0 && collections.Count == 0)
            {
                if (File.Exists(sidecar))
                    File.Delete(sidecar);
            }
            else
            {
                await serializer.SaveFolder(sidecar, files, collections);
            }
        }

        /// <summary>Applies the file's sidecar overrides to the DB: each overridden definition's
        /// file-level canonical rows (from any provider) are replaced by the user's value(s).</summary>
        private async Task ApplySidecarOverrides(IndexedFile indexedFile)
        {
            var overrides = (await LoadOverridesForFile(indexedFile)).Where(a => a.SubResource == null).ToList();
            if (overrides.Count == 0)
                return;

            foreach (var defGroup in overrides.GroupBy(DefId))
            {
                RemoveCanonicalForDefinition(indexedFile.Id, defGroup.Key);
                foreach (var attribute in defGroup)
                {
                    attribute.File = indexedFile;
                    attribute.ProviderId = SidecarProvider.ToString();
                    attribute.Editable = true;
                    StoreNormalized(attribute);
                }
            }

            await dbContext.SaveChangesAsync();
        }

        private void RemoveCanonicalForDefinition(int fileId, int definitionId)
        {
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.SubResourceId == null && a.AttributeDefinitionId == definitionId));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.SubResourceId == null && a.AttributeDefinitionId == definitionId));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.SubResourceId == null && a.AttributeDefinitionId == definitionId));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.SubResourceId == null && a.AttributeDefinitionId == definitionId));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.SubResourceId == null && a.AttributeDefinitionId == definitionId));
        }

        private static void AddBaseline(Dictionary<int, HashSet<string>> baseline, AttributeBase attribute)
        {
            if (attribute.SubResource != null)
                return;
            int def = DefId(attribute);
            if (!baseline.TryGetValue(def, out var set))
                baseline[def] = set = new HashSet<string>();
            set.Add(CanonicalValueKey(attribute));
        }

        /// <summary>An attribute's definition id, reading the navigation property when the FK column
        /// hasn't been fixed up yet (freshly factory-built, untracked attributes).</summary>
        private static int DefId(AttributeBase attribute) =>
            attribute.AttributeDefinition?.Id ?? attribute.AttributeDefinitionId;

        #endregion

        private async Task<IEnumerable<AttributeBase>> LoadMetaFile(string filePath)
        {
            string sidecar = GetSidecarPath(filePath);
            if (!File.Exists(sidecar))
                return Enumerable.Empty<AttributeBase>();

            var folder = await serializer.LoadFolder(sidecar);
            return folder.TryGetValue(Path.GetFileName(filePath), out var attributes)
                ? attributes
                : Enumerable.Empty<AttributeBase>();
        }

        private static string GetSidecarPath(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            return Path.Combine(dir, SidecarFileName);
        }

        /// <summary>
        /// Locates the sidecar and key for an indexed file's overrides (collection_plan.md §8). A
        /// filesystem file is keyed by its filename in its own folder's sidecar; an archive entry — whose
        /// archive is read-only — is keyed by an "archive.zip!/internal/path" locator in the sidecar of the
        /// archive's containing folder.
        /// </summary>
        private (string SidecarPath, string Key) SidecarLocation(IndexedFile file)
        {
            if (file.Source == FileSource.ArchiveEntry)
            {
                int bang = file.Path.IndexOf("!/", StringComparison.Ordinal);
                string archiveRel = bang >= 0 ? file.Path[..bang] : file.Path;
                string internalPath = bang >= 0 ? file.Path[(bang + 2)..] : string.Empty;
                string archiveName = Path.GetFileName(archiveRel.Replace('\\', '/'));
                string archiveDisk = fileService.Resolve(archiveRel);
                string folder = Path.GetDirectoryName(archiveDisk) ?? string.Empty;
                return (Path.Combine(folder, SidecarFileName), archiveName + "!/" + internalPath);
            }

            string diskPath = fileService.Resolve(file.Path);
            return (GetSidecarPath(diskPath), Path.GetFileName(diskPath));
        }

        /// <summary>An indexed file's sidecar overrides, resolved via <see cref="SidecarLocation"/> so it
        /// works for both filesystem files and archive entries.</summary>
        private async Task<IEnumerable<AttributeBase>> LoadOverridesForFile(IndexedFile file)
        {
            var (sidecar, key) = SidecarLocation(file);
            if (!File.Exists(sidecar))
                return Enumerable.Empty<AttributeBase>();

            var folder = await serializer.LoadFolder(sidecar);
            return folder.TryGetValue(key, out var attributes) ? attributes : Enumerable.Empty<AttributeBase>();
        }

        public bool IsMetaFile(string fileName)
        {
            return Path.GetFileName(fileName).Equals(SidecarFileName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
