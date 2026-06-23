using Librarian.Search;
using Librarian.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Librarian.Controllers.Api
{
    /// <summary>
    /// JSON full-text search over file content and text metadata. The MVC
    /// <see cref="Controllers.SearchController"/> renders the same results as HTML.
    /// </summary>
    [ApiController]
    [Route("api/search")]
    [Produces(MediaTypeNames.Application.Json)]
    public class SearchApiController : ControllerBase
    {
        private readonly SearchService searchService;

        public SearchApiController(SearchService searchService)
        {
            this.searchService = searchService;
        }

        [HttpGet]
        public async Task<ActionResult<SearchResponse>> Search(
            [FromQuery(Name = "q")] string? q,
            [FromQuery] bool content = true,
            [FromQuery] bool metadata = true,
            [FromQuery] string? path = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var response = await searchService.SearchAsync(new SearchRequest
            {
                Query = q ?? string.Empty,
                IncludeContent = content,
                IncludeMetadata = metadata,
                PathPrefix = path,
                Page = page,
                PageSize = pageSize,
            });

            return response;
        }
    }
}
