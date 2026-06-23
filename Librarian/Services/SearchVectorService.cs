using Librarian.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Librarian.Services
{
    /// <summary>
    /// Populates the PostgreSQL full-text-search vectors (<c>IndexedFileContents.ContentSearch</c>
    /// and <c>TextAttributes.ValueSearch</c>) from the stored text, using the languages from
    /// configuration plus the language-agnostic <c>simple</c> dictionary.
    ///
    /// The vectors are built server-side with <c>to_tsvector</c>: those functions only translate
    /// inside SQL, so we run a parameterised <c>UPDATE</c> rather than constructing the vector in
    /// C#. Keeping the columns stored (and GIN-indexed) means search queries never have to
    /// re-tokenise the text.
    /// </summary>
    public class SearchVectorService
    {
        // Postgres text-search configuration names are plain identifiers; validate before
        // inlining them into SQL so a misconfigured language can never inject.
        private static readonly Regex ValidLanguage = new("^[a-z_][a-z0-9_]*$", RegexOptions.Compiled);

        private readonly DatabaseContext db;
        private readonly IReadOnlyList<string> languages;

        public SearchVectorService(DatabaseContext db, IConfiguration config)
        {
            this.db = db;

            var configured = config.GetSection("Languages").Get<string[]>() ?? Array.Empty<string>();
            var langs = configured
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim().ToLowerInvariant())
                .Where(l => ValidLanguage.IsMatch(l))
                .ToList();

            // "simple" is always included so text in unconfigured languages is still searchable.
            if (!langs.Contains("simple"))
                langs.Add("simple");

            languages = langs;
        }

        /// <summary>The text-search configurations used, in priority order (e.g. for ts_headline).</summary>
        public IReadOnlyList<string> Languages => languages;

        // EF1002: the only interpolated parts are validated language identifiers and trusted
        // quoted column names (see VectorExpr); the file id is passed as a real parameter.
#pragma warning disable EF1002

        /// <summary>Updates the search vectors for a single file's content and text attributes.</summary>
        public async Task UpdateFileVectorsAsync(int fileId)
        {
            await db.Database.ExecuteSqlRawAsync(
                $@"UPDATE ""IndexedFileContents"" SET ""ContentSearch"" = {VectorExpr("\"Content\"")} WHERE ""FileId"" = {{0}}",
                fileId);

            await db.Database.ExecuteSqlRawAsync(
                $@"UPDATE ""TextAttributes"" SET ""ValueSearch"" = {VectorExpr("\"Value\"")} WHERE ""FileId"" = {{0}}",
                fileId);
        }

        /// <summary>Rebuilds every search vector. Used for an initial backfill or after changing languages.</summary>
        public async Task<int> RebuildAllAsync()
        {
            int contents = await db.Database.ExecuteSqlRawAsync(
                $@"UPDATE ""IndexedFileContents"" SET ""ContentSearch"" = {VectorExpr("\"Content\"")}");

            int attributes = await db.Database.ExecuteSqlRawAsync(
                $@"UPDATE ""TextAttributes"" SET ""ValueSearch"" = {VectorExpr("\"Value\"")}");

            return contents + attributes;
        }
#pragma warning restore EF1002

        /// <summary>
        /// Builds <c>to_tsvector(lang, coalesce(column,'')) || ...</c> across all configured
        /// languages. The languages are validated identifiers; <paramref name="column"/> is a
        /// trusted quoted column reference, never user input.
        /// </summary>
        private string VectorExpr(string column) =>
            string.Join(" || ", languages.Select(l => $"to_tsvector('{l}', coalesce({column}, ''))"));
    }
}
