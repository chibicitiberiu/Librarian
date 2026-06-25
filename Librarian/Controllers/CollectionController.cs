using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Librarian.DB;
using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly MetadataService metadataService;
        private readonly DatabaseContext db;

        public CollectionController(ILogger<CollectionController> logger, CollectionService collections,
                                    MetadataService metadataService, DatabaseContext db)
        {
            this.logger = logger;
            this.collections = collections;
            this.metadataService = metadataService;
            this.db = db;
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

        /// <summary>Tier-0 save of collection-level metadata edits into the collection folder's
        /// <c>.librarian.meta</c> (survives reindex). Form fields are named "<c>Group/Name</c>".</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id)
        {
            try
            {
                var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id);
                if (collection is null)
                    return NotFound($"Collection {id} not found.");

                var edits = new List<MetadataService.UserMetadataEdit>();
                foreach (var key in Request.Form.Keys)
                {
                    int slash = key.IndexOf('/');
                    if (slash <= 0 || slash >= key.Length - 1)
                        continue;   // not a "Group/Name" field (hidden id / antiforgery token)

                    string group = key[..slash];
                    string name = key[(slash + 1)..];
                    var values = Request.Form[key].Where(v => v != null).Select(v => v!).ToList();
                    edits.Add(new MetadataService.UserMetadataEdit(group, name, values));
                }

                await metadataService.SaveCollectionEditsAsync(collection, edits);
                return RedirectToAction("Index", new { id });
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Error saving collection {Id}", id);
                return StatusCode(500, ex.Message);
            }
        }
    }
}
