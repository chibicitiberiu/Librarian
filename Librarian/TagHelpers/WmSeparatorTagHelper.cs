using System;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>A divider. <c>orientation="vertical"</c> (default) is a tiled bar for toolbars/menubars;
    /// <c>orientation="horizontal"</c> is an engraved rule between stacked bars.</summary>
    [HtmlTargetElement("wm-separator")]
    public class WmSeparatorTagHelper : TagHelper
    {
        public string? Orientation { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            bool horizontal = string.Equals(Orientation, "horizontal", StringComparison.OrdinalIgnoreCase);
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(horizontal ? "wm-separator wm-separator-horizontal" : "wm-separator wm-separator-vertical");
            output.Content.SetHtmlContent(string.Empty);
        }
    }
}
