using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NpgsqlTypes;
using System.Collections.Generic;
using System.Linq;

namespace Librarian.Utils
{
    public static class LanguageHelper
    {
        public static NpgsqlTsVector CreateTsVector(string text, IConfiguration config)
        {
            var languages = config.GetSection("Languages").Get<string[]>();
            List<NpgsqlTsVector> vectors = new();

            if (languages != null)
            {
                foreach (var language in languages)
                    vectors.Add(EF.Functions.ToTsVector(language, text));
            }

            vectors.Add(EF.Functions.ToTsVector("simple", text));

            return vectors.Aggregate((x, y) => x.Concat(y));
        }

        public static NpgsqlTsQuery CreateTsQuery(string text, IConfiguration config)
        {
            var languages = config.GetSection("Languages").Get<string[]>();
            List<NpgsqlTsQuery> queries = new();

            if (languages != null)
            {
                foreach (var language in languages)
                    queries.Add(EF.Functions.ToTsQuery(language, text));
            }

            queries.Add(EF.Functions.ToTsQuery("simple", text));

            return queries.Aggregate((x, y) => x.Or(y));
        }
    }
}
