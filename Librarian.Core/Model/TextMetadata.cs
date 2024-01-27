using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class TextMetadata : MetadataBase
    {
        [Required]
        public string Value { get; set; } = null!;

        public NpgsqlTsVector? ValueSearch { get; set; }

        public TextMetadata() { }

        public TextMetadata(MetadataAttributeDefinition attributeDefinition, string value, Guid providerId, bool editable)
            : base(attributeDefinition, providerId, editable)
        {
            Value = value;
        }
    }
}
