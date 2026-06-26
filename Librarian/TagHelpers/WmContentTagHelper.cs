using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>The scrolling main content area of a window. Grows to fill the space between the bars
    /// and the status bar.</summary>
    [HtmlTargetElement("wm-content")]
    public class WmContentTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-window-content");
        }
    }
}
