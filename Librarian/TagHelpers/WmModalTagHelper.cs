using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>
    /// Modal dialog drawn over the desktop on a dimming backdrop (hidden until opened by JS — e.g. a
    /// menu item or button carrying <c>data-wm-open-modal="theId"</c>). The body is the child content;
    /// optional action buttons report a result (<c>confirm</c>/<c>ok</c>/<c>cancel</c>) to listeners of
    /// the <c>wm:modalresult</c> event.
    /// <code>&lt;wm-modal id="m" title="…" ok-text="OK"&gt; … &lt;/wm-modal&gt;</code>
    /// </summary>
    [HtmlTargetElement("wm-modal")]
    public class WmModalTagHelper : TagHelper
    {
        public string? Title { get; set; }
        public string? Icon { get; set; }

        /// <summary>Show the title-bar close (×) button. Default true.</summary>
        public bool Closable { get; set; } = true;

        /// <summary>Primary action label (e.g. "Save"). Renders a default-styled button → result "confirm".</summary>
        public string? ConfirmText { get; set; }

        /// <summary>Renders an OK button → result "ok" (for informational dialogs).</summary>
        public string? OkText { get; set; }

        /// <summary>Renders a Cancel button → result "cancel".</summary>
        public string? CancelText { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var body = await output.GetChildContentAsync();

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-modal-backdrop");
            output.Attributes.SetAttribute("aria-hidden", "true");

            var head = new StringBuilder();
            head.Append("<div class=\"wm-modal\" role=\"dialog\" aria-modal=\"true\">");
            head.Append("<div class=\"wm-titlebar\"><div class=\"wm-titlebar-inner\">");
            if (!string.IsNullOrEmpty(Icon))
                head.Append($"<img class=\"wm-titlebar-icon\" src=\"{Enc(Icon)}\" alt=\"\" />");
            head.Append($"<span class=\"wm-titlebar-text\">{Enc(Title)}</span>");
            if (Closable)
                head.Append("<button type=\"button\" class=\"wm-titlebar-close\" data-wm-close title=\"Close\">×</button>");
            head.Append("</div></div>");
            head.Append("<div class=\"wm-modal-body\">");

            var tail = new StringBuilder();
            tail.Append("</div>"); // close .wm-modal-body
            if (!string.IsNullOrEmpty(ConfirmText) || !string.IsNullOrEmpty(OkText) || !string.IsNullOrEmpty(CancelText))
            {
                tail.Append("<div class=\"wm-modal-actions\">");
                if (!string.IsNullOrEmpty(CancelText))
                    tail.Append($"<button type=\"button\" class=\"wm-button\" data-wm-result=\"cancel\">{Enc(CancelText)}</button>");
                if (!string.IsNullOrEmpty(OkText))
                    tail.Append($"<button type=\"button\" class=\"wm-button\" data-wm-result=\"ok\">{Enc(OkText)}</button>");
                if (!string.IsNullOrEmpty(ConfirmText))
                    tail.Append($"<button type=\"button\" class=\"wm-button wm-button-default\" data-wm-result=\"confirm\">{Enc(ConfirmText)}</button>");
                tail.Append("</div>");
            }
            tail.Append("</div>"); // close .wm-modal

            output.PreContent.SetHtmlContent(head.ToString());
            output.Content.SetHtmlContent(body);
            output.PostContent.SetHtmlContent(tail.ToString());
        }
    }
}
