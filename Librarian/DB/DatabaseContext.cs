using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Librarian.DB
{
    public class DatabaseContext : DbContext
    {
        public DbSet<IndexedFile> IndexedFiles { get; set; }
        public DbSet<IndexedFileContents> IndexedFileContents { get; set; }
        public DbSet<MetadataAttribute> MetadataAttributes { get; set; }
        public DbSet<TextMetadata> TextMetadata { get; set; }
        public DbSet<IntegerMetadata> IntegerMetadata { get; set; }
        public DbSet<FloatMetadata> FloatMetadata { get; set; }
        public DbSet<DateMetadata> DateMetadata { get; set; }
        public DbSet<BlobMetadata> BlobMetadata { get; set; }

        protected DatabaseContext()
        {
        }

        public DatabaseContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexedFile>()
                .HasIndex(f => f.Path).IsUnique(true);

            modelBuilder.Entity<MetadataAttribute>()
                .HasIndex(nameof(MetadataAttribute.Group), nameof(MetadataAttribute.Name), nameof(MetadataAttribute.Type)).IsUnique(true);

            base.OnModelCreating(modelBuilder);
        }
    }
}
