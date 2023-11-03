using Librarian.Metadata;
using System.Collections;
using System.Collections.Generic;

namespace Librarian.ViewModels
{
    public class MetadataViewModel
    {
        public string Path { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string DisplayPath { get; set; } = null!;

        public string? ParentPath { get; set; }

        public bool IsDirectory { get; set; }

        public IEnumerable<MetadataField> Metadata { get; set; } = null!;
    }
}
