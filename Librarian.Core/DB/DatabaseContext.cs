using Librarian.Data;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;
#if DEBUG_DATABASE
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
#endif

namespace Librarian.DB
{
    public class DatabaseContext : DbContext
    {
        public DbSet<IndexedFile> IndexedFiles { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<IndexedFileContents> IndexedFileContents { get; set; }
        public DbSet<AttributeDefinition> AttributeDefinitions { get; set; }
        public DbSet<AttributeAlias> AttributeAliases { get; set; }
        public DbSet<SubResource> SubResources { get; set; }
        public DbSet<RawMetadataAttribute> RawMetadataAttributes { get; set; }

        public DbSet<BlobAttribute> BlobAttributes { get; set; }
        public DbSet<DateAttribute> DateAttributes { get; set; }
        public DbSet<FloatAttribute> FloatAttributes { get; set; }
        public DbSet<IntegerAttribute> IntegerAttributes { get; set; }
        public DbSet<TextAttribute> TextAttributes { get; set; }

        protected DatabaseContext()
        {
        }

        public DatabaseContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
#if DEBUG_DATABASE
            optionsBuilder
                .LogTo(Console.WriteLine)
                .EnableSensitiveDataLogging();
#endif
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexedFile>()
                .HasIndex(f => f.Path).IsUnique(true);

            modelBuilder.Entity<IndexedFile>()
                .HasIndex(f => f.ItemId);
            modelBuilder.Entity<IndexedFile>()
                .HasOne(f => f.Item)
                .WithMany(i => i.Files)
                .HasForeignKey(f => f.ItemId)
                // Deleting an Item just unassociates its files; the association pass re-evaluates.
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AttributeDefinition>()
                .HasIndex(nameof(AttributeDefinition.Group), nameof(AttributeDefinition.Name))
                .IsUnique(true);
            modelBuilder.Entity<AttributeDefinition>()
                .HasData(Datasets.GetMetadataAttributes());

            modelBuilder.Entity<AttributeAlias>()
                .HasIndex(nameof(AttributeAlias.Alias))
                .IsUnique(true);
            modelBuilder.Entity<AttributeAlias>()
                .HasData(Datasets.GetAliases());

            modelBuilder.Entity<RawMetadataAttribute>()
                .HasIndex(nameof(RawMetadataAttribute.FileId));
            modelBuilder.Entity<RawMetadataAttribute>()
                .HasIndex(nameof(RawMetadataAttribute.Namespace), nameof(RawMetadataAttribute.Key));

            base.OnModelCreating(modelBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetConverter>();
        }
    }
}
