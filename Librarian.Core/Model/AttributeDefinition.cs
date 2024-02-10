using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Librarian.Model
{
    public class AttributeDefinition
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(120), NotNull]
        public string Name { get; set; } = null!;

        [MaxLength(255)]
        public string? Group { get; set; }

        public AttributeType Type { get; set; }

        [MaxLength(1024)]
        public string? Description { get; set; }

        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Unit of measurement (e.g. dB).
        /// </summary>
        public string? Unit { get; set; }

        public AttributeDefinition()
        {
        }

        public AttributeDefinition(string name,
                                   string? group,
                                   AttributeType type,
                                   string? description = null,
                                   bool isReadOnly = false,
                                   string? unit = null)
        {
            Name = name;
            Group = group;
            Type = type;
            Description = description;
            IsReadOnly = isReadOnly;
            Unit = unit;
        }

        public AttributeDefinition(int id,
                                   string name,
                                   string? group,
                                   AttributeType type,
                                   string? description = null,
                                   bool isReadOnly = false,
                                   string? unit = null)
        {
            Id = id;
            Name = name;
            Group = group;
            Type = type;
            Description = description;
            IsReadOnly = isReadOnly;
            Unit = unit;
        }
    }
}
