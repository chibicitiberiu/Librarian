using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class BlobMetadata : MetadataAttributeBase
    {
        [Required]
        public byte[] Value { get; set; } = null!;

        public BlobMetadata() { }

        public BlobMetadata(MetadataAttributeDefinition attributeDefinition,
                            byte[] value,
                            Guid providerId,
                            string? providerAttributeId = null,
                            bool editable = false,
                            bool canSaveToFile = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable, canSaveToFile)
        {
            Value = value;
        }
    }
}
