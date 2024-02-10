using Librarian.Model;
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

        public IEnumerable<AttributeBase> Metadata { get; set; } = null!;

        public Dictionary<SubResource, IEnumerable<AttributeBase>> SubResourceMetadata { get; set; } = null!;
    }
}
