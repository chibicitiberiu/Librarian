using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
        public DbSet<Collection> Collections { get; set; }
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

            // --- Containment & collections (collection_plan.md §3, §5) ---

            // Archive entries as virtual files: self-referential parent/children. Deleting an archive
            // drops its entries (cascade). The canonical locator is (ParentFileId, InternalPath).
            modelBuilder.Entity<IndexedFile>()
                .HasOne(f => f.ParentFile)
                .WithMany(f => f.Children)
                .HasForeignKey(f => f.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IndexedFile>()
                .HasIndex(f => new { f.ParentFileId, f.InternalPath }).IsUnique(true);

            // Collection-owned files (collection-level art/nfo). Deleting a collection unassociates them.
            modelBuilder.Entity<IndexedFile>()
                .HasIndex(f => f.CollectionId);
            modelBuilder.Entity<IndexedFile>()
                .HasOne(f => f.Collection)
                .WithMany(c => c.Files)
                .HasForeignKey(f => f.CollectionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Recursive collections: self-ref parent/children. Restrict so a non-empty parent can't be
            // silently deleted; the association pass re-parents in code.
            modelBuilder.Entity<Collection>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentCollectionId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Collection>()
                .HasIndex(c => c.ParentCollectionId);
            modelBuilder.Entity<Collection>()
                .HasIndex(c => c.SourcePath);

            // Items belong to a collection; deleting the collection just unassociates them.
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.ParentCollectionId);
            modelBuilder.Entity<Item>()
                .HasOne(i => i.ParentCollection)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.ParentCollectionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Each typed attribute table gains an optional Collection owner (collection_plan.md §3.3,
            // §5.3). Exactly one of {FileId, CollectionId} is set — enforced by a DB check constraint
            // declared in PostgresDatabaseContext (provider-specific SQL) plus a writer guard.
            ConfigureCollectionAttribute<TextAttribute>(modelBuilder, f => f.TextMetadata, c => c.TextMetadata);
            ConfigureCollectionAttribute<IntegerAttribute>(modelBuilder, f => f.IntegerMetadata, c => c.IntegerMetadata);
            ConfigureCollectionAttribute<FloatAttribute>(modelBuilder, f => f.FloatMetadata, c => c.FloatMetadata);
            ConfigureCollectionAttribute<DateAttribute>(modelBuilder, f => f.DateMetadata, c => c.DateMetadata);
            ConfigureCollectionAttribute<BlobAttribute>(modelBuilder, f => f.BlobMetadata, c => c.BlobMetadata);

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

        /// <summary>
        /// Configures a typed attribute table to be ownable by either an <see cref="IndexedFile"/> or a
        /// <see cref="Collection"/> (collection_plan.md §3.3). Both relationships cascade-delete so that
        /// deleting a file or a collection removes its attributes — the File side was implicitly Cascade
        /// when <see cref="AttributeBase.FileId"/> was [Required]; making it nullable would otherwise flip
        /// the inferred default, so we pin it here.
        /// </summary>
        private static void ConfigureCollectionAttribute<TAttr>(
            ModelBuilder modelBuilder,
            Expression<Func<IndexedFile, IEnumerable<TAttr>?>> fileNav,
            Expression<Func<Collection, IEnumerable<TAttr>?>> collectionNav)
            where TAttr : AttributeBase
        {
            modelBuilder.Entity<TAttr>()
                .HasOne(a => a.File)
                .WithMany(fileNav)
                .HasForeignKey(a => a.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TAttr>()
                .HasIndex(a => a.CollectionId);
            modelBuilder.Entity<TAttr>()
                .HasOne(a => a.Collection)
                .WithMany(collectionNav)
                .HasForeignKey(a => a.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetConverter>();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            GuardAttributeOwners();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, System.Threading.CancellationToken cancellationToken = default)
        {
            GuardAttributeOwners();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Belt-and-suspenders for the DB check constraint (collection_plan.md §3.3): every attribute being
        /// written must be owned by exactly one of an <see cref="IndexedFile"/> or a <see cref="Collection"/>.
        /// We check the navigation as well as the FK so an attribute attached to a not-yet-inserted owner
        /// (FK still 0/unset) still counts as owned.
        /// </summary>
        private void GuardAttributeOwners()
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<AttributeBase>())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                bool hasFile = entry.Entity.FileId != null
                    || entry.Reference(nameof(AttributeBase.File)).CurrentValue != null;
                bool hasCollection = entry.Entity.CollectionId != null
                    || entry.Reference(nameof(AttributeBase.Collection)).CurrentValue != null;

                if (hasFile == hasCollection)
                    throw new InvalidOperationException(
                        $"{entry.Entity.GetType().Name} must be owned by exactly one of File or Collection " +
                        $"(FileId={entry.Entity.FileId}, CollectionId={entry.Entity.CollectionId}).");
            }
        }
    }
}
