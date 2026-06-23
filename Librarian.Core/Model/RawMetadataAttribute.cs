using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    /// <summary>
    /// A single metadata value exactly as reported by a provider, before normalization.
    /// This is the lossless "raw layer" and the source of truth from which the canonical
    /// attributes are derived, so promotion rules can be changed and re-applied without
    /// re-reading the file.
    /// </summary>
    public class RawMetadataAttribute
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey(nameof(File)), Required]
        public int FileId { get; set; }

        [ForeignKey(nameof(SubResource))]
        public int? SubResourceId { get; set; }

        /// <summary>
        /// Metadata schema/source the value came from (e.g. "dc", "exif", "ffmpeg", "file").
        /// </summary>
        [MaxLength(255)]
        public string Namespace { get; set; } = string.Empty;

        /// <summary>Raw key as reported by the provider.</summary>
        [MaxLength(1024), Required]
        public string Key { get; set; } = null!;

        /// <summary>Raw value as reported by the provider.</summary>
        public string Value { get; set; } = null!;

        [Required]
        public string? ProviderId { get; set; }

        public DateTimeOffset DateUpdated { get; set; } = DateTimeOffset.UtcNow;

        #region Foreign keys
        public virtual IndexedFile? File { get; set; }
        public virtual SubResource? SubResource { get; set; }
        #endregion
    }
}
