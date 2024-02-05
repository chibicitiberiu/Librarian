using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class TextMetadata : MetadataAttributeBase
    {
        [Required]
        public string Value { get; set; } = null!;

        public NpgsqlTsVector? ValueSearch { get; set; }

        public TextMetadata() { }

        public TextMetadata(MetadataAttributeDefinition attributeDefinition,
                            string value,
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
