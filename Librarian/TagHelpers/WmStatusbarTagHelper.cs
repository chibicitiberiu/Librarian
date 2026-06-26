using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>A window's bottom status bar (distinct from the desktop taskbar).</summary>
    [HtmlTargetElement("wm-statusbar")]
    public class WmStatusbarTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-statusbar");
        }
    }
}
