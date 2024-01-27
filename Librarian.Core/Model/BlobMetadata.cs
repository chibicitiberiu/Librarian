using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class BlobMetadata : MetadataBase
    {
        [Required]
        public byte[] Value { get; set; } = null!;

        public BlobMetadata() { }

        public BlobMetadata(MetadataAttributeDefinition attributeDefinition, byte[] value, Guid providerId, bool editable = false)
            : base(attributeDefinition, providerId, editable)
        {
            Value = value;
        }
    }
}
