using System.Collections.Generic;
using Librarian.Library;

namespace Librarian.Models
{
    /// <summary>A virtual folder at a drill level (a distinct attribute value).</summary>
    public class LibraryFolderViewModel
    {
        public string Name { get; set; } = null!;
        public string Url { get; set; } = null!;
        public int Count { get; set; }
        public string IconUrl { get; set; } = null!;
    }

    public class LibraryViewModel
    {
        public LibraryCategory Category { get; set; } = null!;

        /// <summary>The active facet view, or null at the category root (view list).</summary>
        public CategoryView? View { get; set; }

        /// <summary>The drill values chosen so far (one per consumed level).</summary>
        public string[] Selected { get; set; } = System.Array.Empty<string>();

        public bool IsLeaf { get; set; }

        /// <summary>Label of the level currently shown as folders (e.g. "Album artist").</summary>
        public string? LevelLabel { get; set; }

        public IReadOnlyList<LibraryFolderViewModel> Folders { get; set; } = System.Array.Empty<LibraryFolderViewModel>();
        public IReadOnlyList<BrowseFileViewModel> Files { get; set; } = System.Array.Empty<BrowseFileViewModel>();

        // (name, url)
        public IReadOnlyList<(string Name, string Url)> Breadcrumbs { get; set; } = System.Array.Empty<(string, string)>();

        /// <summary>Drill-up target, or null at the category root.</summary>
        public string? ParentUrl { get; set; }

        /// <summary>Name shown in the window title / status (current level's value or category name).</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>e.g. <c>library://Music/Pink Floyd</c> — the category analogue of a path.</summary>
        public string LibraryUri { get; set; } = null!;
    }
}
