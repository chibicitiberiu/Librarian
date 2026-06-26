using System.Collections.Generic;

namespace Librarian.ViewModels
{
    /// <summary>Backing model for the /uitest browse-clone: folder identity, breadcrumbs and the
    /// FileListView payload (items + columns + current view mode / zoom).</summary>
    public class UiTestViewModel
    {
        public string DisplayName { get; set; } = "";
        public string Path { get; set; } = "";
        public string? ParentPath { get; set; }

        // (name, path) pairs for the breadcrumb trail.
        public IReadOnlyList<(string Name, string Path)> Breadcrumbs { get; set; } = new List<(string, string)>();

        public IReadOnlyList<WmListItem> Items { get; set; } = new List<WmListItem>();
        public IReadOnlyList<WmListColumn> Columns { get; set; } = new List<WmListColumn>();

        public string Mode { get; set; } = "details";
        public int Zoom { get; set; } = 2;
        public string Sort { get; set; } = "name";

        public int Count => Items.Count;
    }
}
