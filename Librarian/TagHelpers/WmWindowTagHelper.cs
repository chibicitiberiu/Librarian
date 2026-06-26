using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>
    /// Window — the base chrome container. Renders a title bar (icon + centred text) followed by its
    /// children in document order (menubar, toolbar, breadcrumb, &lt;wm-content&gt;, statusbar).
    /// <code>&lt;wm-window title="Browse" icon="/icons/16/file-manager.png"&gt; … &lt;/wm-window&gt;</code>
    /// </summary>
    [HtmlTargetElement("wm-window")]
    public class WmWindowTagHelper : TagHelper
    {
        public string? Title { get; set; }
        public string? Icon { get; set; }

        /// <summary>Render the title bar in its inactive (grayscale) state — used when a modal sits on top.</summary>
        public bool Inactive { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var children = await output.GetChildContentAsync();

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(Inactive ? "wm-window wm-window-inactive" : "wm-window");
            output.Attributes.SetAttribute("tabindex", "-1");

            var tb = new StringBuilder();
            tb.Append("<div class=\"wm-titlebar\"><div class=\"wm-titlebar-inner\">");
            if (!string.IsNullOrEmpty(Icon))
                tb.Append($"<img class=\"wm-titlebar-icon\" src=\"{Enc(Icon)}\" alt=\"\" />");
            tb.Append($"<span class=\"wm-titlebar-text\">{Enc(Title)}</span>");
            tb.Append("</div></div>");

            output.PreContent.SetHtmlContent(tb.ToString());
            output.Content.SetHtmlContent(children);
        }
    }
}
