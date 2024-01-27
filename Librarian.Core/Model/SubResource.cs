using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Librarian.Model
{
    public class SubResource
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey(nameof(File))]
        public int FileId { get; set; }

        public string Name { get; set; } = null!;

        public long? InternalId { get; set; }

        public SubResourceKind Kind { get; set; }

        #region Foreign keys

        /// <summary>
        /// Associated file
        /// </summary>
        public virtual IndexedFile File { get; set; } = null!;

        #endregion
    }
}
