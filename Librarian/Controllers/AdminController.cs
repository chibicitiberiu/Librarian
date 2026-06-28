using Librarian.DB;
using Librarian.Metadata.Normalization;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Librarian.Controllers
{
    /// <summary>HTML control panel for the maintenance operations that previously existed only as JSON
    /// API endpoints (MetadataAdminController). Each action runs synchronously and redirects back with a
    /// one-line result banner — Tier-0 friendly (plain POST forms, no JS required).</summary>
    public class AdminController : Controller
    {
        private readonly RenormalizationService renormalizationService;
        private readonly SearchVectorService searchVectors;
        private readonly ItemAssociationService itemAssociation;
        private readonly IndexingService indexingService;
        private readonly ChecksumService checksumService;
        private readonly DatabaseContext db;
        private readonly MetadataNormalizer normalizer;
        private readonly IConfiguration config;
        private readonly FileService fileService;
        private readonly Librarian.Jobs.JobTracker jobTracker;

        public AdminController(RenormalizationService renormalizationService,
                               SearchVectorService searchVectors,
                               ItemAssociationService itemAssociation,
                               IndexingService indexingService,
                               ChecksumService checksumService,
                               DatabaseContext db,
                               MetadataNormalizer normalizer,
                               IConfiguration config,
                               FileService fileService,
                               Librarian.Jobs.JobTracker jobTracker)
        {
            this.renormalizationService = renormalizationService;
            this.searchVectors = searchVectors;
            this.itemAssociation = itemAssociation;
            this.indexingService = indexingService;
            this.checksumService = checksumService;
            this.db = db;
            this.normalizer = normalizer;
            this.config = config;
            this.fileService = fileService;
            this.jobTracker = jobTracker;
        }

        public async Task<IActionResult> Index()
        {
            // Top unmapped (namespace, key) pairs: group/aggregate in SQL, filter by IsMapped in memory.
            var grouped = await db.RawMetadataAttributes
                .GroupBy(r => new { r.Namespace, r.Key })
                .Select(g => new { g.Key.Namespace, g.Key.Key, Count = g.Count(), Sample = g.Max(r => r.Value) })
                .ToListAsync();
            var unmapped = grouped
                .Where(g => !normalizer.IsMapped(g.Namespace, g.Key))
                .OrderByDescending(g => g.Count)
                .ToList();

            var vm = new AdminViewModel
            {
                TotalFiles = await db.IndexedFiles.CountAsync(f => f.Exists),
                NeedsUpdating = await db.IndexedFiles.CountAsync(f => f.NeedsUpdating && f.Exists),
                Incomplete = await db.IndexedFiles.CountAsync(f => f.ExtractionIncomplete && f.Exists),
                Items = await db.Items.CountAsync(),
                Collections = await db.Collections.CountAsync(),
                DuplicateSets = (await checksumService.GetDuplicatesAsync()).Count,
                ChecksumMode = config["Checksum:Mode"] ?? "Off",
                LibraryDirectory = config["BaseDirectory"],
                LibraryDirectoryResolved = fileService.BasePath,
                LastIndexed = await db.IndexedFiles.MaxAsync(f => (DateTimeOffset?)f.IndexLastUpdated),
                JobRunning = !jobTracker.Jobs.IsEmpty,
                RunningJob = jobTracker.Jobs.Values.FirstOrDefault()?.Name,
                UnmappedTotal = unmapped.Count,
                UnmappedKeys = unmapped.Take(15)
                    .Select(g => new UnmappedKey(g.Namespace, g.Key, g.Count, g.Sample))
                    .ToList(),
                Message = TempData["AdminMessage"] as string,
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reindex(bool force = false)
        {
            await indexingService.IndexAll(force);
            var r = await itemAssociation.AssociateAllAsync();
            TempData["AdminMessage"] = $"Reindex complete ({(force ? "forced" : "incremental")}): "
                + $"{r.Items} items, {r.Sidecars} sidecars, {r.Companions} companions, {r.Promotions} promotions.";
            return RedirectToAction("Index");
        }

        /// <summary>Complete rebuild of all derived data: force-re-extract every file (idempotently
        /// replacing its metadata), prune files no longer on disk, then re-associate items/collections,
        /// renormalize, and rebuild search. The system of record (files + sidecars) is untouched.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RebuildAll()
        {
            await indexingService.IndexAll(force: true);     // force re-extract + prune missing
            var r = await itemAssociation.AssociateAllAsync();
            int collections = await db.Collections.CountAsync();
            int renorm = await renormalizationService.RenormalizeAllAsync();
            int vectors = await searchVectors.RebuildAllAsync();
            TempData["AdminMessage"] = $"Full rebuild complete: re-extracted every file, "
                + $"{r.Items} items, {collections} collections, {renorm} attributes renormalized, "
                + $"{vectors} search vectors.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Associate()
        {
            var r = await itemAssociation.AssociateAllAsync();
            TempData["AdminMessage"] = $"Associated: {r.Items} items, {r.Sidecars} sidecars, {r.Companions} companions.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renormalize()
        {
            int produced = await renormalizationService.RenormalizeAllAsync();
            TempData["AdminMessage"] = $"Renormalized: {produced} canonical attributes rebuilt from raw metadata.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReindexSearch()
        {
            int updated = await searchVectors.RebuildAllAsync();
            TempData["AdminMessage"] = $"Rebuilt {updated} full-text-search vectors.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReindexIncomplete()
        {
            int reindexed = await indexingService.ReindexIncompleteAsync();
            TempData["AdminMessage"] = $"Re-indexed {reindexed} file(s) whose extraction was incomplete.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checksum(string? mode)
        {
            ChecksumMode? parsed = Enum.TryParse<ChecksumMode>(mode, true, out var m) ? m : null;
            var r = await checksumService.RunAsync(parsed);
            TempData["AdminMessage"] = $"Checksum ({r.Mode}): scanned {r.Scanned}, prefix-hashed {r.PrefixHashed}, "
                + $"full-hashed {r.FullHashed}; {r.DuplicateSets} duplicate set(s), {r.DuplicateFiles} file(s).";
            return RedirectToAction("Index");
        }

        /// <summary>Persists the library root to the user settings file. Validated against the filesystem
        /// first: an invalid root would fail startup verification (the app exits), so a non-existent path is
        /// never saved. Takes effect on the next restart (the root is read once when the app starts).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveLibraryDirectory(string? libraryDirectory)
        {
            string? value = libraryDirectory?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                TempData["AdminMessage"] = "Library directory cannot be empty.";
                return RedirectToAction("Index");
            }

            string resolved;
            try
            {
                resolved = PathUtils.GetCanonicalPath(value);
            }
            catch (Exception ex)
            {
                TempData["AdminMessage"] = $"Invalid library directory: {ex.Message}";
                return RedirectToAction("Index");
            }

            if (!Directory.Exists(resolved))
            {
                TempData["AdminMessage"] = $"Library directory does not exist: {resolved}";
                return RedirectToAction("Index");
            }

            try
            {
                PersistSetting("BaseDirectory", value);
            }
            catch (Exception ex)
            {
                TempData["AdminMessage"] = $"Could not save settings: {ex.Message}";
                return RedirectToAction("Index");
            }

            bool changed = !string.Equals(resolved, fileService.BasePath, StringComparison.Ordinal);
            TempData["AdminMessage"] = changed
                ? $"Library directory saved. Restart Librarian to use: {resolved}"
                : $"Library directory saved ({resolved}).";
            return RedirectToAction("Index");
        }

        /// <summary>Merges one key into the writable settings file (AppDataDirectory/settings.json), which
        /// is loaded as the highest-precedence configuration source at startup.</summary>
        private void PersistSetting(string key, string value)
        {
            string dir = fileService.AppDataPath;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.json");

            var settings = new Dictionary<string, string>();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(path));
                    if (existing != null)
                        settings = existing;
                }
                catch
                {
                    // Corrupt or unexpected shape — start fresh rather than fail the save.
                }
            }

            settings[key] = value;
            System.IO.File.WriteAllText(path,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
