using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class BlobAttribute : AttributeBase
    {
        [Required]
        public byte[] Value { get; set; } = null!;

        public BlobAttribute() { }

        public BlobAttribute(AttributeDefinition attributeDefinition,
                             byte[] value,
                             Guid? providerId,
                             string? providerAttributeId = null,
                             bool editable = false)
            : base(attributeDefinition, providerId, providerAttributeId, editable)
        {
            Value = value;
        }

        public override void Update(AttributeBase other)
        {
            if (other is not BlobAttribute otherBlob)
                throw new ArgumentException("Argument is not a " + nameof(BlobAttribute));

            Value = otherBlob.Value;

            base.Update(other);
        }
    }
}
