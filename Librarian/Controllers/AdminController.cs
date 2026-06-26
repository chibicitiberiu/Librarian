using Librarian.DB;
using Librarian.Metadata.Normalization;
using Librarian.Services;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
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

        public AdminController(RenormalizationService renormalizationService,
                               SearchVectorService searchVectors,
                               ItemAssociationService itemAssociation,
                               IndexingService indexingService,
                               ChecksumService checksumService,
                               DatabaseContext db,
                               MetadataNormalizer normalizer,
                               IConfiguration config)
        {
            this.renormalizationService = renormalizationService;
            this.searchVectors = searchVectors;
            this.itemAssociation = itemAssociation;
            this.indexingService = indexingService;
            this.checksumService = checksumService;
            this.db = db;
            this.normalizer = normalizer;
            this.config = config;
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
    }
}
