using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    /// <summary>
    /// The catalogued unit (plan.md Standing decisions): one primary file plus its companions/sidecars, grouped
    /// from a folder. A book, an album track, a game install — each is one Item. Browsing and faceting
    /// operate on Items; the Item Viewer (M6) drills an Item to its files. The Item's primary is the
    /// member file with <see cref="FileRole.Primary"/>; its canonical attributes live on that primary
    /// (sidecar metadata is promoted onto it).
    /// </summary>
    public class Item
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Auto (owned by the heuristic) vs Manual (a user correction the pass must preserve).</summary>
        public RoleSource RoleSource { get; set; } = RoleSource.Auto;

        /// <summary>The structural Collection this Item belongs to (e.g. the Season of an episode), or
        /// null if the Item is not part of any collection (collection_plan.md §3.2).</summary>
        [ForeignKey(nameof(ParentCollection))]
        public int? ParentCollectionId { get; set; }
        public virtual Collection? ParentCollection { get; set; }

        /// <summary>The files that make up this Item (one is the primary; the rest sidecars/companions).</summary>
        public virtual ICollection<IndexedFile> Files { get; set; } = null!;
    }
}
