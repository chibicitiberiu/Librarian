using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;

namespace Librarian.Model
{
    public class TextMetadata : MetadataBase
    {
        [Required]
        public string Value { get; set; } = null!;

        public NpgsqlTsVector? ValueSearch { get; set; }
    }
}
