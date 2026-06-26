using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>Layout primitives: <c>&lt;wm-hbox&gt;</c> (row) and <c>&lt;wm-vbox&gt;</c> (column).
    /// Add <c>fill="true"</c> to make the box grow to fill its parent.</summary>
    [HtmlTargetElement("wm-hbox")]
    [HtmlTargetElement("wm-vbox")]
    public class WmBoxTagHelper : TagHelper
    {
        public bool Fill { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            bool vbox = output.TagName == "wm-vbox";
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(vbox ? "wm-layout-vbox" : "wm-layout-hbox");
            if (Fill)
                output.AddClass("wm-layout-fill");
        }
    }
}
