using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class FloatMetadata : MetadataBase
    {
        [Required]
        public double Value { get; set; }

        public FloatMetadata() { }

        public FloatMetadata(MetadataAttributeDefinition attributeDefinition, double value, Guid providerId, bool editable = false)
            : base(attributeDefinition, providerId, editable)
        {
            Value = value;
        }
    }
}
