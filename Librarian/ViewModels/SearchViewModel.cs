using Librarian.Search;

namespace Librarian.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;
        public bool IncludeContent { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public string? PathPrefix { get; set; }

        /// <summary>Null until a search has actually been run (i.e. the page was opened with a query).</summary>
        public SearchResponse? Results { get; set; }
    }
}
