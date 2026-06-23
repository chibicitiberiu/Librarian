using System.Collections.Generic;

namespace Librarian.Search
{
    /// <summary>A full-text search request over file content and/or text metadata.</summary>
    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;

        public bool IncludeContent { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>Optional structured filter: only return files whose path starts with this prefix.</summary>
        public string? PathPrefix { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    /// <summary>Raw projection of one result row as returned by the search SQL.</summary>
    public class SearchHit
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public double Rank { get; set; }
        public bool InContent { get; set; }
        public bool InMetadata { get; set; }
        public string? Snippet { get; set; }
        public long TotalCount { get; set; }
    }

    /// <summary>A single search result presented to the UI/API.</summary>
    public class SearchResultItem
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Rank { get; set; }
        public bool InContent { get; set; }
        public bool InMetadata { get; set; }

        /// <summary>Highlighted content fragment (HTML, already sanitised) or matching metadata value.</summary>
        public string? Snippet { get; set; }
    }

    /// <summary>A page of search results plus paging metadata.</summary>
    public class SearchResponse
    {
        public string Query { get; set; } = string.Empty;
        public IReadOnlyList<SearchResultItem> Results { get; set; } = new List<SearchResultItem>();
        public long TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalPages => PageSize > 0 ? (int)((TotalCount + PageSize - 1) / PageSize) : 0;
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }
}
