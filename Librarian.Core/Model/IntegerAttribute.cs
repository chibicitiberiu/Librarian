using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class IntegerAttribute : AttributeBase
    {
        [Required]
        public long Value { get; set; }

        public IntegerAttribute() { }

        public IntegerAttribute(AttributeDefinition attributeDefinition,
                                long value,
                                Guid? providerId,
                                string? providerAttributeId = null,
                                bool editable = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable)
        {
            Value = value;
        }

        public override void Update(AttributeBase other)
        {
            if (other is not IntegerAttribute otherInt)
                throw new ArgumentException("Argument is not a " + nameof(IntegerAttribute));

            Value = otherInt.Value;

            base.Update(other);
        }
    }
}
