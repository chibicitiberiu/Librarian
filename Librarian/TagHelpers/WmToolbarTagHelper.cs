using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>A toolbar: a horizontal strip of buttons and separators. Buttons placed directly inside
    /// stack their icon over their text.</summary>
    [HtmlTargetElement("wm-toolbar")]
    public class WmToolbarTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-toolbar");
        }
    }
}
