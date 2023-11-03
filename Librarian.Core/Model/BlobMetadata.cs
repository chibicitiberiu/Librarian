using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class BlobMetadata : MetadataBase
    {
        [Required]
        public byte[] Value { get; set; } = null!;
    }
}
