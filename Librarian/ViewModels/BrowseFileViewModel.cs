using System;

namespace Librarian.Models
{
    /// <summary>A single file/folder entry used by the Library facet browser (and previously the old
    /// Browse listing). The Browse page itself now renders through the wm FileListView (<see
    /// cref="Librarian.ViewModels.WmListItem"/>); this lighter shape is retained for the Library views.</summary>
    public class BrowseFileViewModel
    {
        public string Name { get; set; } = null!;

        public string Path { get; set; } = null!;

        public bool IsDirectory { get; set; }

        public DateTimeOffset LastModified { get; set; }

        public string? MimeType { get; set; }

        public long? Size { get; set; }

        public string? DisplaySize { get; set; }

        public string IconUrl { get; set; } = null!;
    }
}
