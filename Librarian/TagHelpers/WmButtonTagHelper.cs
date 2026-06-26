using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>
    /// Button — optional icon + text. Renders a real &lt;button&gt; by default, or an &lt;a&gt; when
    /// <c>href</c> is set. A <c>tooltip</c> (falling back to the text) becomes the title attribute.
    /// <code>&lt;wm-button icon="/icons/32/edit-cut.png" text="Cut" tooltip="Cut selection" /&gt;</code>
    /// </summary>
    [HtmlTargetElement("wm-button")]
    public class WmButtonTagHelper : TagHelper
    {
        public string? Icon { get; set; }
        public string? Text { get; set; }
        public string? Href { get; set; }
        public string? Tooltip { get; set; }
        public bool Disabled { get; set; }

        /// <summary>The &lt;button&gt; type (button/submit/reset). Ignored when rendered as a link.</summary>
        [HtmlAttributeName("type")]
        public string? ButtonType { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var inner = (await output.GetChildContentAsync()).GetContent();
            bool isLink = !string.IsNullOrEmpty(Href);

            output.TagName = isLink ? "a" : "button";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(Disabled ? "wm-button disabled" : "wm-button");

            if (isLink)
            {
                output.Attributes.SetAttribute("href", Href!);
            }
            else
            {
                output.Attributes.SetAttribute("type", string.IsNullOrEmpty(ButtonType) ? "button" : ButtonType);
                if (Disabled)
                    output.Attributes.SetAttribute("disabled", "disabled");
            }

            var tip = !string.IsNullOrEmpty(Tooltip) ? Tooltip : Text;
            if (!string.IsNullOrEmpty(tip))
                output.Attributes.SetAttribute("title", tip);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Icon))
                sb.Append($"<img class=\"wm-button-icon\" src=\"{Enc(Icon)}\" alt=\"\" />");
            if (!string.IsNullOrEmpty(Text))
                sb.Append($"<span class=\"wm-button-text\">{Enc(Text)}</span>");
            sb.Append(inner);

            output.Content.SetHtmlContent(sb.ToString());
        }
    }
}
