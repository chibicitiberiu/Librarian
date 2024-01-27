using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Librarian.Model
{
    public class MetadataAttributeDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(120), NotNull]
        public string Name { get; set; } = null!;

        [MaxLength(255)]
        public string? Group { get; set; }

        public MetadataType Type { get; set; }

        [MaxLength(1024)]
        public string? Description { get; set; }

        public MetadataAttributeDefinition()
        {
        }

        public MetadataAttributeDefinition(string name, string? group, MetadataType type, string? description = null)
        {
            Name = name;
            Group = group;
            Type = type;
            Description = description;
        }

        public MetadataAttributeDefinition(int id, string name, string? group, MetadataType type, string? description = null)
        {
            Id = id;
            Name = name;
            Group = group;
            Type = type;
            Description = description;
        }
    }
}
