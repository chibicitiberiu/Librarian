using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class DateAttribute : AttributeBase
    {
        [Required]
        public DateTimeOffset Value { get; set; }

        public DateAttribute() { }

        public DateAttribute(AttributeDefinition attributeDefinition,
                             DateTimeOffset value,
                             Guid? providerId,
                             string? providerAttributeId = null,
                             bool editable = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable)
        {
            Value = value;
        }

        public override void Update(AttributeBase other)
        {
            if (other is not DateAttribute otherDate)
                throw new ArgumentException("Argument is not a " + nameof(DateAttribute));

            Value = otherDate.Value;

            base.Update(other);
        }
    }
}
