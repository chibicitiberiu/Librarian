using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    public class MetadataAttributeAlias
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Alias { get; set; } = null!;

        [ForeignKey(nameof(AttributeDefinition))]
        public int? AttributeDefinitionId { get; set; }

        public AliasRole Role { get; set; } = AliasRole.Default;

        #region Foreign keys

        public MetadataAttributeDefinition? AttributeDefinition { get; set; }

        #endregion
    }
}
