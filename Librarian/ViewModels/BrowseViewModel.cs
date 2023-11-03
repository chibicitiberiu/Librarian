using Librarian.Controllers;
using System;
using System.Collections.Generic;

namespace Librarian.Models
{
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

    public class BrowseViewModel
    {
        public string Path { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string DisplayPath { get; set; } = null!;

        public string? ParentPath { get; set; }

        public IEnumerable<BrowseFileViewModel> Files { get; set; } = null!;

        // name, path
        public IEnumerable<(string, string)> Breadcrumbs { get; set; } = null!;

        public BrowseClipboardModel? Clipboard { get; set;}
    }
}
