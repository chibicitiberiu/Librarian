using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class IntegerMetadata : MetadataBase
    {
        [Required]
        public long Value { get; set; }

        public IntegerMetadata() { }

        public IntegerMetadata(MetadataAttributeDefinition attributeDefinition, long value, Guid providerId, bool editable = false)
            : base(attributeDefinition, providerId, editable)
        {
            Value = value;
        }
    }
}
