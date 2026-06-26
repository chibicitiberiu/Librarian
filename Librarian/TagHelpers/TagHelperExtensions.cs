using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Librarian.TagHelpers
{
    internal static class TagHelperExtensions
    {
        /// <summary>Prepends a control's own class(es) ahead of any author-supplied class attribute, so
        /// callers can add extra classes without losing the component's base class.</summary>
        public static void AddClass(this TagHelperOutput output, string cls)
        {
            var existing = output.Attributes["class"]?.Value?.ToString();
            output.Attributes.SetAttribute("class",
                string.IsNullOrEmpty(existing) ? cls : cls + " " + existing);
        }

        /// <summary>HTML-encodes a value for safe interpolation into attributes / text.</summary>
        public static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
