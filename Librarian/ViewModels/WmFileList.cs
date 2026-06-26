using System;
using System.Collections.Generic;

namespace Librarian.ViewModels
{
    /// <summary>A column shown in the FileListView's details mode (beyond the always-present Name column).</summary>
    public class WmListColumn
    {
        public WmListColumn(string header, bool numeric = false)
        {
            Header = header;
            Numeric = numeric;
        }

        public string Header { get; }

        /// <summary>Right-aligned and sorted numerically when true.</summary>
        public bool Numeric { get; }
    }

    /// <summary>One cell value, with an optional separate sort key (e.g. raw byte count behind "18.3K").</summary>
    public class WmListCell
    {
        public WmListCell(string? display, string? sort = null)
        {
            Display = display;
            Sort = sort;
        }

        public string? Display { get; }
        public string? Sort { get; }
    }

    /// <summary>
    /// A single row/tile/icon in the FileListView. The same item renders across all view modes; modes
    /// just pick which parts to show (cells for details, ContentLine for tiles, etc.).
    /// </summary>
    public class WmListItem
    {
        public string Name { get; set; } = "";

        /// <summary>Where the item's name links to (navigation). Null for non-navigable rows.</summary>
        public string? Href { get; set; }

        /// <summary>Small (16px) type icon, always available — used by the details table.</summary>
        public string IconUrl { get; set; } = "";

        /// <summary>Larger (48px) type icon used by list/tile/icon modes so it stays crisp when zoomed.</summary>
        public string? LargeIconUrl { get; set; }

        /// <summary>Optional larger preview (e.g. the image itself) used by tile/icon modes; falls back to the icon.</summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>Library-relative path — used as the selection key and by the context menu.</summary>
        public string Path { get; set; } = "";

        /// <summary>Folders / collections sort before files and may be styled differently.</summary>
        public bool IsContainer { get; set; }

        /// <summary>The single descriptive line shown under the name in tile mode.</summary>
        public string? ContentLine { get; set; }

        /// <summary>Detail-column values, positionally aligned with the list's columns.</summary>
        public IReadOnlyList<WmListCell> Cells { get; set; } = Array.Empty<WmListCell>();
    }
}
