using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Librarian.Util
{
    public static class AttributeHelper
    {
        private static readonly Regex SnakeCaseRegex = new(@"^[_\-]?([a-z]+|[A-Z]+)([_\-]([a-z]+|[A-Z]+))*$");

        /// <summary>
        /// Cleans up an attribute name, can also convert case from camel/pascal/snake to "Sentence case".
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string? NormalizeAttributeName(string? name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return null;

            HandleSnakeCase(ref name);

            return name;
        }

        private static void HandleSnakeCase(ref string name)
        {
            var snake_case = SnakeCaseRegex.Match(name);
            if (snake_case?.Success == true)
            {
                var words = name.Split('_', '-')
                        .Select(x => x.ToLowerInvariant());

                name = string.Join(' ', words);

                // make first word uppercase
                name = string.Concat(name[..1].ToUpperInvariant(), name.AsSpan(1));
            }
        }
    }
}
