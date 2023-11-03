using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class IntegerMetadata : MetadataBase
    {
        [Required]
        public long Value { get; set; }
    }
}
