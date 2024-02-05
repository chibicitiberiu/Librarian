using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class FloatMetadata : MetadataAttributeBase
    {
        [Required]
        public double Value { get; set; }

        public FloatMetadata() { }

        public FloatMetadata(MetadataAttributeDefinition attributeDefinition,
                             double value,
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
