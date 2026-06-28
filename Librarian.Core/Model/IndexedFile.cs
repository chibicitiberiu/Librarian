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

        #region Physical / addressing layer (collection_plan.md §3.1)

        /// <summary>Whether this file's bytes live on disk (<see cref="FileSource.Filesystem"/>) or inside
        /// an archive (<see cref="FileSource.ArchiveEntry"/>). Disk I/O routes on this.</summary>
        public FileSource Source { get; set; } = FileSource.Filesystem;

        /// <summary>For an <see cref="FileSource.ArchiveEntry"/>, the archive <see cref="IndexedFile"/> this
        /// entry lives in (null for filesystem files). Deleting the archive cascades to its entries.</summary>
        [ForeignKey(nameof(ParentFile))]
        public int? ParentFileId { get; set; }
        public virtual IndexedFile? ParentFile { get; set; }

        /// <summary>The archive's own entries (empty for a non-archive file).</summary>
        public virtual ICollection<IndexedFile> Children { get; set; } = new List<IndexedFile>();

        /// <summary>Path within the archive, e.g. "Disc1/03.flac". The canonical locator is
        /// (<see cref="ParentFileId"/>, <see cref="InternalPath"/>) under a composite unique index;
        /// <see cref="Path"/> stays a synthesized display string only. Null for filesystem files.</summary>
        [MaxLength(4096)]
        public string? InternalPath { get; set; }

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

        /// <summary>The Collection that owns this file directly (collection-level art/nfo), as opposed to
        /// belonging to an Item. A file is owned by at most one of <see cref="Item"/> or
        /// <see cref="Collection"/> (collection_plan.md §3.2).</summary>
        [ForeignKey(nameof(Collection))]
        public int? CollectionId { get; set; }
        public virtual Collection? Collection { get; set; }

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

        /// <summary>Content-detected MIME type (file --mime-type / Tika), set during indexing. Authoritative
        /// over the file extension for media-class classification (robust to mismatched extensions). Null
        /// until detected (e.g. a directory entry, or before the first extraction).</summary>
        public string? MimeType { get; set; }

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
