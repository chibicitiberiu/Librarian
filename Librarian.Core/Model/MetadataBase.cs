using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class MetadataBase
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey(nameof(File)), Required]
        public int? FileId { get; set; }

        [ForeignKey(nameof(SubResource))]
        public int? SubResourceId { get; set; }

        [ForeignKey(nameof(AttributeDefinition))]
        public int AttributeDefinitionId { get; set; }

        /// <summary>
        /// If true, this metadata is editable
        /// </summary>
        public bool Editable { get; set; }

        /// <summary>
        /// ID of the provider that created this metadata.
        /// Information is used for editing the metadata.
        /// </summary>
        public string ProviderId { get; set; }

        #region Foreign keys
        /// <summary>
        /// Associated file
        /// </summary>
        public virtual IndexedFile? File { get; set; }

        /// <summary>
        /// Associated file
        /// </summary>
        public virtual SubResource? SubResource { get; set; }

        /// <summary>
        /// Metadata attribute
        /// </summary>
        public virtual MetadataAttributeDefinition AttributeDefinition { get; set; } = null!;
        #endregion

        public MetadataBase() { }

        public MetadataBase(MetadataAttributeDefinition attributeDefinition, Guid providerId, bool editable = false)
        {
            AttributeDefinition = attributeDefinition;
            ProviderId = providerId.ToString();
            Editable = editable;
        }
    }
}
