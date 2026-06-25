using Librarian.DB;
using Librarian.Metadata.Normalization;
using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Librarian.Controllers.Api
{
    [ApiController]
    [Route("api/metadata")]
    [Produces(MediaTypeNames.Application.Json)]
    public class MetadataAdminController : ControllerBase
    {
        private readonly RenormalizationService renormalizationService;
        private readonly SearchVectorService searchVectors;
        private readonly ItemAssociationService itemAssociation;
        private readonly IndexingService indexingService;
        private readonly DatabaseContext db;
        private readonly MetadataNormalizer normalizer;

        public MetadataAdminController(RenormalizationService renormalizationService,
                                       SearchVectorService searchVectors,
                                       ItemAssociationService itemAssociation,
                                       IndexingService indexingService,
                                       DatabaseContext db,
                                       MetadataNormalizer normalizer)
        {
            this.renormalizationService = renormalizationService;
            this.searchVectors = searchVectors;
            this.itemAssociation = itemAssociation;
            this.indexingService = indexingService;
            this.db = db;
            this.normalizer = normalizer;
        }

        /// <summary>
        /// Full re-index. With <c>force=true</c> (default) every file is re-extracted even if
        /// unchanged, rebuilding the canonical layer from scratch and flushing any stale rows; then
        /// re-groups Items. Long-running.
        /// </summary>
        [HttpPost("reindex")]
        public async Task<IActionResult> Reindex([FromQuery] bool force = true)
        {
            await indexingService.IndexAll(force);
            var result = await itemAssociation.AssociateAllAsync();
            return Ok(new { force, items = result.Items, result.Sidecars, result.Companions, result.Promotions });
        }

        /// <summary>
        /// Rebuilds canonical attributes from the stored raw metadata using the current
        /// rules, without re-reading any files.
        /// </summary>
        [HttpPost("renormalize")]
        public async Task<IActionResult> Renormalize()
        {
            int produced = await renormalizationService.RenormalizeAllAsync();
            return Ok(new { reprocessed = produced });
        }

        /// <summary>
        /// Groups indexed files into Items (a primary plus its sidecars/companions) and promotes
        /// catalogue metadata onto each Item's primary. Re-runnable; rebuilds from scratch.
        /// </summary>
        [HttpPost("associate")]
        public async Task<IActionResult> Associate()
        {
            var result = await itemAssociation.AssociateAllAsync();
            return Ok(result);
        }

        /// <summary>
        /// Rebuilds every full-text-search vector (content + text attributes) from the stored
        /// text. Useful after backfilling existing data or changing the configured languages.
        /// </summary>
        [HttpPost("reindex-search")]
        public async Task<IActionResult> ReindexSearch()
        {
            int updated = await searchVectors.RebuildAllAsync();
            return Ok(new { updated });
        }

        /// <summary>
        /// Returns distinct raw metadata (Namespace, Key) pairs that have no promotion rule,
        /// ordered by row count descending so the most common unmapped keys surface first.
        /// </summary>
        [HttpGet("unmapped")]
        public async Task<IActionResult> GetUnmappedKeys()
        {
            // GroupBy and aggregate in the database, then pull into memory.
            var grouped = await db.RawMetadataAttributes
                .GroupBy(r => new { r.Namespace, r.Key })
                .Select(g => new
                {
                    g.Key.Namespace,
                    g.Key.Key,
                    Count = g.Count(),
                    SampleValue = g.Max(r => r.Value)
                })
                .ToListAsync();

            // Filter with IsMapped in memory — it cannot be translated to SQL.
            var unmapped = grouped
                .Where(g => !normalizer.IsMapped(g.Namespace, g.Key))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Namespace)
                .ThenBy(g => g.Key)
                .Select(g => new
                {
                    g.Namespace,
                    g.Key,
                    g.Count,
                    g.SampleValue
                })
                .ToList();

            return Ok(unmapped);
        }
    }
}
