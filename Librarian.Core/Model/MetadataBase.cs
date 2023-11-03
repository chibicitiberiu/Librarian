using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class MetadataBase
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey(nameof(File))]
        public int FileId { get; set; }

        [ForeignKey(nameof(Attribute))]
        public int AttributeId { get; set; }

        /// <summary>
        /// If true, this metadata is editable
        /// </summary>
        public bool Editable { get; set; }

        /// <summary>
        /// ID of Metadata Provider that created this metadata instance
        /// </summary>
        public int ProviderId { get; set; }

        #region Foreign keys
        /// <summary>
        /// Associated file
        /// </summary>
        public virtual IndexedFile File { get; set; } = null!;

        /// <summary>
        /// Metadata attribute
        /// </summary>
        public virtual MetadataAttribute Attribute { get; set; } = null!;
        #endregion
    }
}
