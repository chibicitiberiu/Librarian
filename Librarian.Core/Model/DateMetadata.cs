using System;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class DateMetadata : MetadataBase
    {
        [Required]
        public DateTimeOffset Value { get; set; }

        public DateMetadata() { }

        public DateMetadata(MetadataAttributeDefinition attributeDefinition, DateTimeOffset value, Guid providerId, bool editable = false)
            : base(attributeDefinition, providerId, editable)
        {
            Value = value;
        }
    }
}
