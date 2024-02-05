using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class MetadataAttributeBase
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
        
        public bool IsUserEdited { get; set; }

        #region Info about source

        /// <summary>
        /// ID of the provider that created this metadata.
        /// Information is used for editing the metadata.
        /// </summary>
        [Required]
        public string? ProviderId { get; set; }

        /// <summary>
        /// Provider specific identifier
        /// </summary>
        public string? ProviderAttributeId { get; set; }

        /// <summary>
        /// If true, the provider is capable of saving this attribute back to the original file
        /// </summary>
        public bool CanSaveToFile { get; set; }

        #endregion

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

        public MetadataAttributeBase() { }

        public MetadataAttributeBase(MetadataAttributeDefinition attributeDefinition,
                            Guid providerId,
                            string? providerAttributeId = null,
                            bool editable = false,
                            bool canSaveToFile = false)
        {
            AttributeDefinition = attributeDefinition;
            ProviderId = providerId.ToString();
            Editable = editable;
            ProviderAttributeId = providerAttributeId;
            CanSaveToFile = canSaveToFile;
        }
    }
}
