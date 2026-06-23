using Librarian.Search;
using Librarian.Services;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Librarian.Controllers
{
    public class SearchController : Controller
    {
        private readonly SearchService searchService;

        public SearchController(SearchService searchService)
        {
            this.searchService = searchService;
        }

        public async Task<IActionResult> Index(
            string? query,
            bool include_metadata = true,
            bool include_content = true,
            string? path = null,
            int page = 1)
        {
            var vm = new SearchViewModel
            {
                Query = query ?? string.Empty,
                IncludeContent = include_content,
                IncludeMetadata = include_metadata,
                PathPrefix = path,
            };

            if (!string.IsNullOrWhiteSpace(query))
            {
                vm.Results = await searchService.SearchAsync(new SearchRequest
                {
                    Query = query,
                    IncludeContent = include_content,
                    IncludeMetadata = include_metadata,
                    PathPrefix = path,
                    Page = page,
                });
            }

            return View(vm);
        }

        public IActionResult Advanced()
        {
            return View();
        }
    }
}
