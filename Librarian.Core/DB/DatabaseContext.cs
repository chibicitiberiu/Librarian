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
        public DbSet<IndexedFileContents> IndexedFileContents { get; set; }
        public DbSet<AttributeDefinition> AttributeDefinitions { get; set; }
        public DbSet<AttributeAlias> AttributeAliases { get; set; }
        public DbSet<SubResource> SubResources { get; set; }

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

            base.OnModelCreating(modelBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetConverter>();
        }
    }
}
