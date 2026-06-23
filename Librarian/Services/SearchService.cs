using Librarian.DB;
using Librarian.Search;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Librarian.Services
{
    /// <summary>
    /// Runs full-text search over extracted content (<c>IndexedFileContents.ContentSearch</c>)
    /// and text metadata (<c>TextAttributes.ValueSearch</c>) in a single PostgreSQL query.
    /// Matches from both sources are unioned, grouped per file, ranked with <c>ts_rank</c>, and
    /// paged. The vectors are GIN-indexed (see the model configuration), so the <c>@@</c> match
    /// uses the index.
    /// </summary>
    public class SearchService
    {
        // Sentinels wrapped around content matches by ts_headline. They contain no HTML-special
        // characters, so after HTML-encoding the fragment we swap them for <mark> tags — the
        // surrounding content text stays encoded and safe to render.
        private const string HlOn = "@@HLON@@";
        private const string HlOff = "@@HLOFF@@";
        private const string HeadlineOptions =
            "StartSel=" + HlOn + ", StopSel=" + HlOff +
            ", MaxFragments=2, MinWords=4, MaxWords=16, ShortWord=2, FragmentDelimiter= … ";

        private const int MaxPageSize = 100;

        private readonly DatabaseContext db;
        private readonly IReadOnlyList<string> languages;

        public SearchService(DatabaseContext db, SearchVectorService vectors)
        {
            this.db = db;
            this.languages = vectors.Languages;
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request)
        {
            int page = Math.Max(1, request.Page);
            int pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

            var response = new SearchResponse
            {
                Query = request.Query,
                Page = page,
                PageSize = pageSize,
            };

            if (string.IsNullOrWhiteSpace(request.Query))
                return response;

            // If the caller selected neither source, search both.
            bool includeContent = request.IncludeContent || !request.IncludeMetadata;
            bool includeMetadata = request.IncludeMetadata || !request.IncludeContent;

            int offset = (page - 1) * pageSize;
            var (sql, parameters) = BuildSql(request, includeContent, includeMetadata, pageSize, offset);

            var hits = await db.Database.SqlQueryRaw<SearchHit>(sql, parameters).ToListAsync();

            response.TotalCount = hits.Count > 0 ? hits[0].TotalCount : 0;
            response.Results = hits.Select(ToResultItem).ToList();
            return response;
        }

        private (string sql, object[] parameters) BuildSql(
            SearchRequest request, bool includeContent, bool includeMetadata, int limit, int offset)
        {
            // {0} is the (single) user-supplied query text, reused for each language dictionary.
            string tsquery = string.Join(" || ",
                languages.Select(l => $"websearch_to_tsquery('{l}', {{0}})"));
            string headlineLang = languages[0];

            var unionParts = new List<string>();
            if (includeContent)
            {
                unionParts.Add($@"
                    SELECT c.""FileId"" AS file_id,
                           ts_rank(c.""ContentSearch"", q.query) AS rank,
                           'content' AS src,
                           ts_headline('{headlineLang}', c.""Content"", q.query, '{HeadlineOptions}') AS snippet
                    FROM ""IndexedFileContents"" c, q
                    WHERE c.""ContentSearch"" @@ q.query");
            }
            if (includeMetadata)
            {
                unionParts.Add(@"
                    SELECT t.""FileId"" AS file_id,
                           ts_rank(t.""ValueSearch"", q.query) AS rank,
                           'metadata' AS src,
                           t.""Value"" AS snippet
                    FROM ""TextAttributes"" t, q
                    WHERE t.""ValueSearch"" @@ q.query");
            }

            var parameters = new List<object> { request.Query };

            string pathFilter = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.PathPrefix))
            {
                parameters.Add(request.PathPrefix.TrimEnd('/') + "/%");
                pathFilter = $@" AND f.""Path"" LIKE {{{parameters.Count - 1}}}";
            }

            string sql = $@"
                WITH q AS (SELECT {tsquery} AS query)
                SELECT f.""Id""                                   AS ""Id"",
                       f.""Path""                                 AS ""Path"",
                       max(hits.rank)::float8                     AS ""Rank"",
                       bool_or(hits.src = 'content')              AS ""InContent"",
                       bool_or(hits.src = 'metadata')             AS ""InMetadata"",
                       (array_agg(hits.snippet ORDER BY hits.rank DESC))[1] AS ""Snippet"",
                       (count(*) OVER ())::bigint                 AS ""TotalCount""
                FROM ( {string.Join("\n                    UNION ALL\n", unionParts)} ) hits
                JOIN ""IndexedFiles"" f ON f.""Id"" = hits.file_id
                WHERE f.""Exists"" = TRUE{pathFilter}
                GROUP BY f.""Id"", f.""Path""
                ORDER BY ""Rank"" DESC, f.""Path""
                LIMIT {limit} OFFSET {offset}";

            return (sql, parameters.ToArray());
        }

        private static SearchResultItem ToResultItem(SearchHit hit)
        {
            string name = hit.Path;
            int slash = hit.Path.LastIndexOf('/');
            if (slash >= 0 && slash < hit.Path.Length - 1)
                name = hit.Path[(slash + 1)..];

            return new SearchResultItem
            {
                Path = hit.Path,
                Name = name,
                Rank = hit.Rank,
                InContent = hit.InContent,
                InMetadata = hit.InMetadata,
                Snippet = BuildSnippet(hit.Snippet),
            };
        }

        /// <summary>HTML-encodes a snippet, then turns the ts_headline sentinels into &lt;mark&gt; tags.</summary>
        private static string? BuildSnippet(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;

            string encoded = HtmlEncoder.Default.Encode(raw);
            return encoded
                .Replace(HlOn, "<mark>")
                .Replace(HlOff, "</mark>");
        }
    }
}
