using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    /// <summary>
    /// A recursive structural grouping above the <see cref="Item"/> level (collection_plan.md §3.2):
    /// Show → Season → Episode, Artist → Album → Track, at arbitrary depth. Unlike an Item (one
    /// primary file + companions, grouped from a single folder), a Collection groups other
    /// collections and items, and owns its own art/metadata — solving the "shared art has no owner"
    /// problem (album covers, <c>folder.jpg</c>, <c>tvshow.nfo</c> attach here instead of floating
    /// as orphan companions). Collection metadata lives in the same typed-attribute tables as file
    /// metadata, keyed by <see cref="AttributeBase.CollectionId"/>.
    /// </summary>
    public class Collection
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Self-reference giving arbitrary nesting (null for a root collection).</summary>
        [ForeignKey(nameof(Parent))]
        public int? ParentCollectionId { get; set; }
        public virtual Collection? Parent { get; set; }
        public virtual ICollection<Collection> Children { get; set; } = new List<Collection>();

        /// <summary>The items that belong directly to this collection (e.g. the episodes of a season).</summary>
        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

        /// <summary>Files owned directly by this collection — collection-level art/nfo (season poster,
        /// <c>tvshow.nfo</c>). Only Sidecar (nfo) and Companion (art) roles are meaningful here.</summary>
        public virtual ICollection<IndexedFile> Files { get; set; } = new List<IndexedFile>();

        public CollectionKind Kind { get; set; } = CollectionKind.Generic;

        /// <summary>Auto (owned by the heuristic, rebuilt each run) vs Manual (a user-pinned
        /// membership/kind the association pass must preserve).</summary>
        public RoleSource RoleSource { get; set; } = RoleSource.Auto;

        /// <summary>Folder or archive path this collection was derived from. Gives a stable identity so a
        /// Manual collection re-binds to the same folder/archive after Auto collections are rebuilt.</summary>
        [MaxLength(4096)]
        public string? SourcePath { get; set; }

        #region Foreign keys (collection-owned attributes)
        public virtual ICollection<TextAttribute> TextMetadata { get; set; } = null!;
        public virtual ICollection<IntegerAttribute> IntegerMetadata { get; set; } = null!;
        public virtual ICollection<FloatAttribute> FloatMetadata { get; set; } = null!;
        public virtual ICollection<DateAttribute> DateMetadata { get; set; } = null!;
        public virtual ICollection<BlobAttribute> BlobMetadata { get; set; } = null!;
        #endregion
    }
}
