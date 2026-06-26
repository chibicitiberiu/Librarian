using Librarian.Model;
using System;
using System.Collections.Generic;

namespace Librarian.ViewModels
{
    /// <summary>One file row in the Item Viewer's "Files in this Item" table.</summary>
    public class ItemFileRow
    {
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        public string IconUrl { get; set; } = null!;

        /// <summary>The designated primary content file of the Item.</summary>
        public bool IsPrimary { get; set; }

        /// <summary>The file currently being viewed (so it can be highlighted).</summary>
        public bool IsCurrent { get; set; }

        /// <summary>True when this file lives inside an archive (a virtual entry) rather than on disk.</summary>
        public bool InArchive { get; set; }
    }

    public class MetadataViewModel
    {
        public string Path { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string DisplayPath { get; set; } = null!;

        public string? ParentPath { get; set; }

        public bool IsDirectory { get; set; }

        public IEnumerable<AttributeBase> Metadata { get; set; } = null!;

        public Dictionary<SubResource, IEnumerable<AttributeBase>> SubResourceMetadata { get; set; } = null!;

        /// <summary>Number of OTHER files that share this file's content hash (0 = none / not hashed).
        /// Surfaced as a badge linking to the duplicates view.</summary>
        public int DuplicateCount { get; set; }

        /// <summary>True when the viewed file is a virtual archive entry; <see cref="ArchiveName"/> is the
        /// containing archive's file name (collection_plan.md §3.1).</summary>
        public bool InArchive { get; set; }
        public string? ArchiveName { get; set; }

        #region Item (plan.md Phase 6d)

        /// <summary>True when this file belongs to a grouped Item (so the "Files in this Item" pane shows).</summary>
        public bool HasItem { get; set; }

        /// <summary>Relative path of the image to preview (the file itself if it's an image, else the
        /// Item's cover art), or null when there's nothing to show.</summary>
        public string? CoverPath { get; set; }

        /// <summary>Editable (non read-only) attribute definitions the user can add to this file, as
        /// (Group, Name) pairs — backs the Item Viewer's "Add field" control.</summary>
        public IReadOnlyList<(string Group, string Name)> AddableFields { get; set; } = Array.Empty<(string, string)>();

        public IReadOnlyList<ItemFileRow> Content { get; set; } = Array.Empty<ItemFileRow>();
        public IReadOnlyList<ItemFileRow> Sidecars { get; set; } = Array.Empty<ItemFileRow>();
        public IReadOnlyList<ItemFileRow> Resources { get; set; } = Array.Empty<ItemFileRow>();

        #endregion

        #region Structural collection (collection_plan.md §9.2)

        /// <summary>"Part of:" breadcrumb up the owning collection chain (root … nearest), empty when the
        /// item belongs to no collection.</summary>
        public IReadOnlyList<(int Id, string Name)> CollectionCrumbs { get; set; } = Array.Empty<(int, string)>();

        #endregion
    }
}
