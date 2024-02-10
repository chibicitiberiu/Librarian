using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public abstract class AttributeBase
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
        /// Date when this attribute was last updated
        /// </summary>
        public DateTimeOffset DateUpdated { get; set; } = DateTimeOffset.UtcNow;

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
        public virtual AttributeDefinition AttributeDefinition { get; set; } = null!;
        #endregion

        protected AttributeBase() { }

        protected AttributeBase(AttributeDefinition attributeDefinition,
                                Guid? providerId,
                                string? providerAttributeId = null,
                                bool editable = false)
        {
            AttributeDefinition = attributeDefinition;
            ProviderId = providerId.ToString();
            Editable = editable;
            ProviderAttributeId = providerAttributeId;
        }

        public virtual void Update(AttributeBase other)
        {
            Editable = other.Editable;
            ProviderAttributeId = other.ProviderAttributeId;
            DateUpdated = DateTimeOffset.UtcNow;
        }
    }
}
