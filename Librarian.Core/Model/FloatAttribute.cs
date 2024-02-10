using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class FloatAttribute : AttributeBase
    {
        [Required]
        public double Value { get; set; }

        public FloatAttribute() { }

        public FloatAttribute(AttributeDefinition attributeDefinition,
                              double value,
                              Guid? providerId,
                              string? providerAttributeId = null,
                              bool editable = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable)
        {
            Value = value;
        }

        public override void Update(AttributeBase other)
        {
            if (other is not FloatAttribute otherFloat)
                throw new ArgumentException("Argument is not a " + nameof(FloatAttribute));

            Value = otherFloat.Value;

            base.Update(other);
        }
    }
}
