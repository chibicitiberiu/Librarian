using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Librarian.Model
{
    public class MetadataAttribute
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(120), NotNull]
        public string Name { get; set; } = null!;

        [MaxLength(255)]
        public string? Group { get; set; }

        public MetadataType Type { get; set; }

        public MetadataAttribute()
        {
        }

        public MetadataAttribute(string name, string? group, MetadataType type)
        {
            Name = name;
            Group = group;
            Type = type;
        }
    }
}
