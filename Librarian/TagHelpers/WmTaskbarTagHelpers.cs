using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>The desktop taskbar — a fixed bottom strip of tasks plus a tray. Replaces the old status bar.</summary>
    [HtmlTargetElement("wm-taskbar")]
    public class WmTaskbarTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-taskbar");
        }
    }

    /// <summary>A taskbar entry (e.g. Browse, Settings). A link when <c>href</c> is set; mark the current one <c>active</c>.</summary>
    [HtmlTargetElement("wm-task")]
    public class WmTaskTagHelper : TagHelper
    {
        public string? Icon { get; set; }
        public string? Text { get; set; }
        public string? Href { get; set; }
        public string? Tooltip { get; set; }
        public bool Active { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var inner = (await output.GetChildContentAsync()).GetContent();
            bool isLink = !string.IsNullOrEmpty(Href);

            output.TagName = isLink ? "a" : "span";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(Active ? "wm-task wm-task-active" : "wm-task");
            if (isLink) output.Attributes.SetAttribute("href", Href!);
            var tip = !string.IsNullOrEmpty(Tooltip) ? Tooltip : Text;
            if (!string.IsNullOrEmpty(tip)) output.Attributes.SetAttribute("title", tip);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Icon))
                sb.Append($"<img src=\"{Enc(Icon)}\" alt=\"\" /> ");
            if (!string.IsNullOrEmpty(Text))
                sb.Append(Enc(Text));
            sb.Append(inner);
            output.Content.SetHtmlContent(sb.ToString());
        }
    }

    /// <summary>The right-hand tray area of the taskbar (notification/indicator icons).</summary>
    [HtmlTargetElement("wm-tray")]
    public class WmTrayTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-tray");
        }
    }

    /// <summary>A tray indicator (icon + optional badge). The jobs indicator (id <c>wm-tray-jobs</c>) is
    /// updated by wm.js polling the jobs summary endpoint.</summary>
    [HtmlTargetElement("wm-tray-icon")]
    public class WmTrayIconTagHelper : TagHelper
    {
        public string? Icon { get; set; }
        public string? Tooltip { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "span";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-tray-icon");
            if (!string.IsNullOrEmpty(Tooltip))
                output.Attributes.SetAttribute("title", Tooltip);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Icon))
                sb.Append($"<img src=\"{Enc(Icon)}\" alt=\"\" />");
            sb.Append("<span class=\"wm-tray-badge\"></span>");
            output.Content.SetHtmlContent(sb.ToString());
        }
    }
}
