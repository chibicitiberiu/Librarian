using Librarian.Data;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;

namespace Librarian.DB
{
    public class DatabaseContext : DbContext
    {
        public DbSet<IndexedFile> IndexedFiles { get; set; }
        public DbSet<IndexedFileContents> IndexedFileContents { get; set; }
        public DbSet<MetadataAttributeDefinition> MetadataAttributes { get; set; }
        public DbSet<MetadataAttributeAlias> AttributeAliases { get; set; }
        public DbSet<SubResource> SubResources { get; set; }

        public DbSet<BlobMetadata> BlobMetadata { get; set; }
        public DbSet<DateMetadata> DateMetadata { get; set; }
        public DbSet<FloatMetadata> FloatMetadata { get; set; }
        public DbSet<IntegerMetadata> IntegerMetadata { get; set; }
        public DbSet<TextMetadata> TextMetadata { get; set; }

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

            modelBuilder.Entity<MetadataAttributeDefinition>()
                .HasIndex(nameof(MetadataAttributeDefinition.Group), nameof(MetadataAttributeDefinition.Name))
                .IsUnique(true);
            modelBuilder.Entity<MetadataAttributeDefinition>()
                .HasData(Datasets.GetMetadataAttributes());

            modelBuilder.Entity<MetadataAttributeAlias>()
                .HasIndex(nameof(MetadataAttributeAlias.Alias))
                .IsUnique(true);
            modelBuilder.Entity<MetadataAttributeAlias>()
                .HasData(Datasets.GetAliases());

            base.OnModelCreating(modelBuilder);
        }
    }
}
