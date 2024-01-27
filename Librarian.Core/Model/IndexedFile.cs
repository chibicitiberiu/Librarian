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

        #endregion

        #region Indexing

        public bool NeedsUpdating { get; set; } = true;
        public DateTimeOffset IndexLastUpdated { get; set; }

        #endregion

        #region Basic file metadata, used to determine if index needs updating

        public long? Size { get; set; } = null;
        public DateTimeOffset Created {  get; set; }
        public DateTimeOffset Modified { get; set; }

        #endregion

        #region Foreign keys
        public virtual IndexedFileContents? Contents { get; set; }
        public virtual ICollection<TextMetadata> TextMetadata { get; set; } = null!;
        public virtual ICollection<IntegerMetadata> IntegerMetadata { get; set; } = null!;
        public virtual ICollection<FloatMetadata> FloatMetadata { get; set; } = null!;
        public virtual ICollection<DateMetadata> DateMetadata { get; set; } = null!;
        public virtual ICollection<BlobMetadata> BlobMetadata { get; set; } = null!;
        #endregion
    }
}
