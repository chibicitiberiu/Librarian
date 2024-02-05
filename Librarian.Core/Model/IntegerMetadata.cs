using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class IntegerMetadata : MetadataAttributeBase
    {
        [Required]
        public long Value { get; set; }

        public IntegerMetadata() { }

        public IntegerMetadata(MetadataAttributeDefinition attributeDefinition,
                               long value,
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
