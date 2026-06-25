using System.Threading.Tasks;
using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Librarian.Controllers
{
    /// <summary>
    /// Browses the structural <see cref="Librarian.Model.Collection"/> tree (collection_plan.md §9): a
    /// Tier-0, server-rendered Collection Viewer that drills Show → Season → Episode by real parent/child
    /// links (not faceted attribute drills), with a landing list of root collections.
    /// </summary>
    public class CollectionController : Controller
    {
        private readonly ILogger<CollectionController> logger;
        private readonly CollectionService collections;

        public CollectionController(ILogger<CollectionController> logger, CollectionService collections)
        {
            this.logger = logger;
            this.collections = collections;
        }

        /// <summary>The Collections landing page: every root collection.</summary>
        public async Task<IActionResult> Roots()
        {
            try
            {
                return View(await collections.GetRootsAsync());
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Error listing root collections");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>One collection: its cover, metadata, breadcrumb, child collections, items and files.</summary>
        public async Task<IActionResult> Index(int id)
        {
            try
            {
                var details = await collections.GetAsync(id);
                if (details is null)
                    return NotFound($"Collection {id} not found.");
                return View(details);
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Error loading collection {Id}", id);
                return StatusCode(500, ex.Message);
            }
        }
    }
}
