using System;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class DateMetadata : MetadataBase
    {
        [Required]
        public DateTimeOffset Value { get; set; }
    }
}
