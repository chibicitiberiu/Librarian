using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class TextAttribute : AttributeBase
    {
        [Required]
        public string Value { get; set; } = null!;

        public NpgsqlTsVector? ValueSearch { get; set; }

        public TextAttribute() { }

        public TextAttribute(AttributeDefinition attributeDefinition,
                             string value,
                             Guid? providerId,
                             string? providerAttributeId = null,
                             bool editable = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable)
        {
            Value = value;
        }

        public override void Update(AttributeBase other)
        {
            if (other is not TextAttribute otherText)
                throw new ArgumentException("Argument is not a " + nameof(TextAttribute));

            Value = otherText.Value;
            ValueSearch = otherText.ValueSearch;

            base.Update(other);
        }
    }
}
