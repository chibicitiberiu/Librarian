using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Librarian.TagHelpers.TagHelperExtensions;

namespace Librarian.TagHelpers
{
    /// <summary>
    /// FileListView — the universal item list (browse, search, …). The same items render in four modes
    /// — <c>details</c> (sortable table), <c>list</c> (columnar), <c>tiles</c> (icon + name + one line)
    /// and <c>icons</c> (big icon over name) — with three zoom levels for the non-table modes. Optional
    /// checkbox selection; Ctrl/Shift multi-select and type-to-jump are added by wm.js.
    /// </summary>
    [HtmlTargetElement("wm-filelist")]
    public class WmFileListTagHelper : TagHelper
    {
        public IEnumerable<WmListItem>? Items { get; set; }
        public IReadOnlyList<WmListColumn>? Columns { get; set; }
        public string Mode { get; set; } = "details";
        public int Zoom { get; set; } = 2;
        public bool Selectable { get; set; } = true;
        public bool ShowCheckboxes { get; set; } = true;
        public string EmptyText { get; set; } = "This folder is empty.";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var items = Items?.ToList() ?? new List<WmListItem>();
            var cols = Columns ?? Array.Empty<WmListColumn>();
            var mode = (Mode ?? "details").ToLowerInvariant();
            if (mode != "list" && mode != "tiles" && mode != "icons") mode = "details";
            int zoom = Math.Clamp(Zoom, 1, 3);

            if (items.Count == 0)
            {
                output.TagName = "div";
                output.TagMode = TagMode.StartTagAndEndTag;
                output.AddClass("wm-filelist wm-filelist-empty");
                output.Content.SetHtmlContent($"<span class=\"muted\">{Enc(EmptyText)}</span>");
                return;
            }

            var rootClass = $"wm-filelist wm-filelist-{mode} wm-filelist-zoom-{zoom}";
            if (Selectable) rootClass += " wm-filelist-selectable";
            if (Selectable && ShowCheckboxes) rootClass += " wm-filelist-checkboxes";

            output.TagMode = TagMode.StartTagAndEndTag;
            output.AddClass(rootClass);
            output.Attributes.SetAttribute("data-wm-filelist", "");
            output.Attributes.SetAttribute("tabindex", "0");

            if (mode == "details")
            {
                output.TagName = "table";
                output.AddClass("sortable");
                output.Content.SetHtmlContent(RenderDetails(items, cols));
            }
            else
            {
                output.TagName = "div";
                output.Content.SetHtmlContent(RenderGrid(items, mode));
            }
        }

        private string Checkbox() =>
            (Selectable && ShowCheckboxes)
                ? "<input type=\"checkbox\" class=\"wm-filelist-check\" aria-label=\"Select\" />"
                : "";

        private string RenderDetails(List<WmListItem> items, IReadOnlyList<WmListColumn> cols)
        {
            var sb = new StringBuilder();
            sb.Append("<thead><tr>");
            if (Selectable && ShowCheckboxes)
                sb.Append("<th class=\"wm-col-check\"><input type=\"checkbox\" class=\"wm-filelist-all\" aria-label=\"Select all\" /></th>");
            sb.Append("<th class=\"wm-col-icon\"></th>");
            sb.Append("<th>Name</th>");
            foreach (var c in cols)
                sb.Append($"<th{(c.Numeric ? " class=\"wm-col-num\"" : "")}>{Enc(c.Header)}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var it in items)
            {
                sb.Append($"<tr class=\"wm-filelist-row wm-filelist-item\" data-path=\"{Enc(it.Path)}\" data-name=\"{Enc(it.Name)}\">");
                if (Selectable && ShowCheckboxes)
                    sb.Append($"<td class=\"wm-col-check\">{Checkbox()}</td>");
                sb.Append($"<td class=\"wm-col-icon\"><img src=\"{Enc(it.IconUrl)}\" alt=\"\" /></td>");
                var nameSort = (it.IsContainer ? "0_" : "1_") + it.Name;
                sb.Append($"<td data-sort=\"{Enc(nameSort)}\">{NameLink(it)}</td>");
                for (int i = 0; i < cols.Count; i++)
                {
                    var cell = i < it.Cells.Count ? it.Cells[i] : null;
                    var sort = cell?.Sort;
                    var cls = cols[i].Numeric ? " class=\"wm-col-num\"" : "";
                    sb.Append("<td");
                    sb.Append(cls);
                    if (sort != null) sb.Append($" data-sort=\"{Enc(sort)}\"");
                    sb.Append('>');
                    sb.Append(Enc(cell?.Display));
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");
            return sb.ToString();
        }

        private string RenderGrid(List<WmListItem> items, string mode)
        {
            var sb = new StringBuilder();
            foreach (var it in items)
            {
                sb.Append($"<div class=\"wm-filelist-item\" data-path=\"{Enc(it.Path)}\" data-name=\"{Enc(it.Name)}\">");
                sb.Append(Checkbox());

                var href = string.IsNullOrEmpty(it.Href) ? "#" : it.Href;
                sb.Append($"<a class=\"wm-filelist-link\" href=\"{Enc(href)}\">");

                if (mode == "list")
                {
                    sb.Append($"<img class=\"wm-filelist-icon-sm\" src=\"{Enc(it.IconUrl)}\" alt=\"\" />");
                    sb.Append($"<span class=\"wm-filelist-label\">{Enc(it.Name)}</span>");
                }
                else
                {
                    var thumb = string.IsNullOrEmpty(it.ThumbnailUrl) ? it.IconUrl : it.ThumbnailUrl;
                    sb.Append($"<img class=\"wm-filelist-thumb\" src=\"{Enc(thumb)}\" alt=\"\" />");
                    if (mode == "tiles")
                    {
                        sb.Append("<span class=\"wm-filelist-tiletext\">");
                        sb.Append($"<span class=\"wm-filelist-label\">{Enc(it.Name)}</span>");
                        if (!string.IsNullOrEmpty(it.ContentLine))
                            sb.Append($"<span class=\"wm-filelist-content\">{Enc(it.ContentLine)}</span>");
                        sb.Append("</span>");
                    }
                    else // icons
                    {
                        sb.Append($"<span class=\"wm-filelist-label\">{Enc(it.Name)}</span>");
                    }
                }

                sb.Append("</a></div>");
            }
            return sb.ToString();
        }

        private string NameLink(WmListItem it)
        {
            if (string.IsNullOrEmpty(it.Href))
                return Enc(it.Name);
            return $"<a href=\"{Enc(it.Href)}\">{Enc(it.Name)}</a>";
        }
    }
}
