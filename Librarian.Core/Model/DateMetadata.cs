using System;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class DateMetadata : MetadataAttributeBase
    {
        [Required]
        public DateTimeOffset Value { get; set; }

        public DateMetadata() { }

        public DateMetadata(MetadataAttributeDefinition attributeDefinition,
                            DateTimeOffset value,
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
