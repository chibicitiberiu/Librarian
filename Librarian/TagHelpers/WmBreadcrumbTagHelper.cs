using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>Shared state passed from &lt;wm-breadcrumb&gt; to its &lt;wm-crumb&gt; children so each
    /// crumb after the first can emit a leading separator.</summary>
    public sealed class WmBreadcrumbContext
    {
        public int Count;
    }

    /// <summary>Breadcrumb / location bar. Holds a trail of &lt;wm-crumb&gt; elements.</summary>
    [HtmlTargetElement("wm-breadcrumb")]
    public class WmBreadcrumbTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            context.Items[typeof(WmBreadcrumbContext)] = new WmBreadcrumbContext();
            var inner = await output.GetChildContentAsync();

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-locationbar");
            output.PreContent.SetHtmlContent("<div class=\"wm-breadcrumb input\">");
            output.Content.SetHtmlContent(inner);
            output.PostContent.SetHtmlContent("</div>");
        }
    }

    /// <summary>A single crumb. The first crumb usually carries an <c>icon</c> (drawn left of the text);
    /// deeper crumbs are text only and get an automatic leading separator.</summary>
    [HtmlTargetElement("wm-crumb")]
    public class WmCrumbTagHelper : TagHelper
    {
        public string? Href { get; set; }
        public string? Icon { get; set; }
        public string? Text { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var inner = (await output.GetChildContentAsync()).GetContent();

            var bc = context.Items.TryGetValue(typeof(WmBreadcrumbContext), out var raw)
                ? raw as WmBreadcrumbContext : null;
            bool first = bc == null || bc.Count == 0;
            if (bc != null) bc.Count++;

            bool isLink = !string.IsNullOrEmpty(Href);
            output.TagName = isLink ? "a" : "span";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-crumb");
            if (isLink)
                output.Attributes.SetAttribute("href", Href!);
            if (!string.IsNullOrEmpty(Text))
                output.Attributes.SetAttribute("title", Text);

            if (!first)
                output.PreElement.SetHtmlContent("<span class=\"wm-crumb-sep\">/</span>");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Icon))
                sb.Append($"<img class=\"wm-crumb-icon\" src=\"{Enc(Icon)}\" alt=\"\" />");
            if (!string.IsNullOrEmpty(Text))
                sb.Append($"<span class=\"wm-crumb-text\">{Enc(Text)}</span>");
            sb.Append(inner);

            output.Content.SetHtmlContent(sb.ToString());
        }
    }
}
