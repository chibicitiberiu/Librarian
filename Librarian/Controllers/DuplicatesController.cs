using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Librarian.Controllers
{
    /// <summary>User-facing view of the dedup pass's results: sets of files that share a full content
    /// hash (collection_plan.md §7.5). The hashing itself is driven by the checksum admin endpoint.</summary>
    public class DuplicatesController : Controller
    {
        private readonly ChecksumService checksumService;

        public DuplicatesController(ChecksumService checksumService)
        {
            this.checksumService = checksumService;
        }

        public async Task<IActionResult> Index()
        {
            var sets = await checksumService.GetDuplicatesAsync();
            return View(sets);
        }
    }
}
