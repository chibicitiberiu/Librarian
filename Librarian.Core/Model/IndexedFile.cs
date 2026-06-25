using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    public class IndexedFile
    {
        #region Identification

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(4096), Required]
        public string Path { get; set; } = null!;

        public bool Exists { get; set; } = true;

        #endregion

        #region Item association (plan.md Standing decisions)

        /// <summary>This file's role within its Item (primary content, metadata sidecar, or resource).</summary>
        public FileRole Role { get; set; } = FileRole.Primary;

        /// <summary>Auto (heuristic) vs Manual (a user correction the association pass must preserve).</summary>
        public RoleSource RoleSource { get; set; } = RoleSource.Auto;

        /// <summary>The Item this file belongs to (null while unassociated, or for an orphan companion
        /// like shared album art with no single owner).</summary>
        [ForeignKey(nameof(Item))]
        public int? ItemId { get; set; }

        public virtual Item? Item { get; set; }

        #endregion

        #region Indexing

        public bool NeedsUpdating { get; set; } = true;
        public DateTimeOffset IndexLastUpdated { get; set; }

        /// <summary>True when the last extraction was cut short by a transient provider failure (e.g.
        /// Tika unreachable) after retries were exhausted: the file's metadata may be partial and a
        /// re-index could complete it. Cleared whenever an extraction finishes without such a failure.</summary>
        public bool ExtractionIncomplete { get; set; }

        #endregion

        #region Basic file metadata, used to determine if index needs updating

        public long? Size { get; set; } = null;
        public DateTimeOffset Created {  get; set; }
        public DateTimeOffset Modified { get; set; }

        #endregion

        #region Content hashing (plan.md Phase 4 — checksums & dedup)

        /// <summary>SHA-256 of the file's first block — the dedup pass's cheap pre-filter for files that
        /// share a size. Null until hashed; cleared when the file changes. The full content hash lives as
        /// the "File attributes/Checksum" canonical attribute, computed lazily (dedup) or always (integrity).</summary>
        public byte[]? PrefixHash { get; set; }

        #endregion

        #region Foreign keys
        public virtual IndexedFileContents? Contents { get; set; }
        public virtual ICollection<RawMetadataAttribute> RawMetadata { get; set; } = null!;
        public virtual ICollection<TextAttribute> TextMetadata { get; set; } = null!;
        public virtual ICollection<IntegerAttribute> IntegerMetadata { get; set; } = null!;
        public virtual ICollection<FloatAttribute> FloatMetadata { get; set; } = null!;
        public virtual ICollection<DateAttribute> DateMetadata { get; set; } = null!;
        public virtual ICollection<BlobAttribute> BlobMetadata { get; set; } = null!;
        #endregion
    }
}
