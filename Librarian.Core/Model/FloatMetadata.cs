using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class FloatMetadata : MetadataBase
    {
        [Required]
        public double Value { get; set; }
    }
}
