using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    public class IndexedFileContents
    {
        [Key, ForeignKey(nameof(File))]
        public int FileId { get; set; }

        public IndexedFile File { get; set; } = null!;

        public string? Content { get; set; }

        public NpgsqlTsVector? ContentSearch { get; set; }
    }
}
