using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>Top-level menu bar: a row of &lt;wm-menu&gt; dropdowns (File, View, Help, …).</summary>
    [HtmlTargetElement("wm-menubar")]
    public class WmMenuBarTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-menubar");
            output.Attributes.SetAttribute("role", "menubar");
        }
    }

    /// <summary>A top-level menu (the label + its dropdown of &lt;wm-menuitem&gt;s). Lives in a menubar.</summary>
    [HtmlTargetElement("wm-menu")]
    public class WmMenuTagHelper : TagHelper
    {
        public string? Text { get; set; }
        public string? Tooltip { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var inner = await output.GetChildContentAsync();

            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-menu-top");

            var head = new StringBuilder();
            head.Append("<button type=\"button\" class=\"wm-menu-label\"");
            var tip = !string.IsNullOrEmpty(Tooltip) ? Tooltip : Text;
            if (!string.IsNullOrEmpty(tip))
                head.Append($" title=\"{Enc(tip)}\"");
            head.Append($">{Enc(Text)}</button>");
            head.Append("<div class=\"wm-menu-popup\" role=\"menu\">");

            output.PreContent.SetHtmlContent(head.ToString());
            output.Content.SetHtmlContent(inner);
            output.PostContent.SetHtmlContent("</div>");
        }
    }

    /// <summary>
    /// A menu entry. Renders as a link (<c>href</c>), an action button (<c>action</c> → data-action for
    /// JS) or static text. Supports an icon or a check mark (<c>checked</c>/<c>checkbox</c>), and becomes
    /// a submenu when it contains nested &lt;wm-menuitem&gt;s.
    /// </summary>
    [HtmlTargetElement("wm-menuitem")]
    public class WmMenuItemTagHelper : TagHelper
    {
        public string? Text { get; set; }
        public string? Icon { get; set; }
        public string? Href { get; set; }
        public string? Tooltip { get; set; }
        public string? Action { get; set; }
        public bool Checkbox { get; set; }
        public bool Checked { get; set; }
        public bool Disabled { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childHtml = (await output.GetChildContentAsync()).GetContent();
            bool hasSubmenu = !string.IsNullOrWhiteSpace(childHtml);

            string tag;
            if (hasSubmenu) tag = "div";
            else if (!string.IsNullOrEmpty(Href) && !Disabled) tag = "a";
            else if (!string.IsNullOrEmpty(Action) && !Disabled) tag = "button";
            else tag = "span";

            output.TagName = tag;
            output.TagMode = TagMode.StartTagAndEndTag;

            var cls = "wm-menuitem";
            if (hasSubmenu) cls += " wm-menuitem-haspopup";
            if (Disabled) cls += " disabled";
            output.AddClass(cls);

            if (tag == "a")
                output.Attributes.SetAttribute("href", Href!);
            if (tag == "button")
            {
                output.Attributes.SetAttribute("type", "button");
                if (!string.IsNullOrEmpty(Action))
                    output.Attributes.SetAttribute("data-action", Action);
            }

            var tip = !string.IsNullOrEmpty(Tooltip) ? Tooltip : Text;
            if (!string.IsNullOrEmpty(tip))
                output.Attributes.SetAttribute("title", tip);

            var sb = new StringBuilder();

            // Left gutter: a check mark (checkbox items) or an icon, else an empty cell to keep labels aligned.
            if (Checkbox || Checked)
                sb.Append($"<span class=\"wm-menuitem-check\">{(Checked ? "✔" : "")}</span>");
            else if (!string.IsNullOrEmpty(Icon))
                sb.Append($"<img class=\"wm-menuitem-icon\" src=\"{Enc(Icon)}\" alt=\"\" />");
            else
                sb.Append("<span class=\"wm-menuitem-check\"></span>");

            sb.Append($"<span class=\"wm-menuitem-text\">{Enc(Text)}</span>");

            if (hasSubmenu)
            {
                sb.Append("<span class=\"wm-menuitem-arrow\">▸</span>");
                sb.Append("<div class=\"wm-menu-popup wm-submenu\" role=\"menu\">");
                sb.Append(childHtml);
                sb.Append("</div>");
            }

            output.Content.SetHtmlContent(sb.ToString());
        }
    }

    /// <summary>A horizontal divider between groups of menu items.</summary>
    [HtmlTargetElement("wm-menu-separator")]
    public class WmMenuSeparatorTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-menu-separator");
            output.Content.SetHtmlContent(string.Empty);
        }
    }

    /// <summary>
    /// A free-standing context menu (hidden until JS positions it at the cursor). Bind it to a host
    /// element with <c>data-wm-contextmenu="theId"</c>; optional <c>data-properties-url</c> drives the
    /// Properties action. Holds &lt;wm-menuitem&gt;s like any menu.
    /// </summary>
    [HtmlTargetElement("wm-contextmenu")]
    public class WmContextMenuTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass("wm-menu-popup wm-contextmenu");
            output.Attributes.SetAttribute("role", "menu");
        }
    }
}
