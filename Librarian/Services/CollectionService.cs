using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Librarian.DB;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;

namespace Librarian.Services
{
    /// <summary>A child collection shown in the Collection Viewer's listing.</summary>
    public record CollectionChild(int Id, string Name, CollectionKind Kind, int ItemCount, int ChildCount);

    /// <summary>An item shown in the Collection Viewer's listing (links to the Item Viewer by its
    /// primary file path).</summary>
    public record CollectionItem(int Id, string Name, string? PrimaryPath, string? CoverPath);

    /// <summary>A file owned directly by a collection (its art/nfo).</summary>
    public record CollectionFile(string Path, string Name, FileRole Role);

    /// <summary>Everything the Collection Viewer needs for one collection.</summary>
    public class CollectionDetails
    {
        public int Id { get; init; }
        public string Name { get; init; } = null!;
        public CollectionKind Kind { get; init; }
        public string? SourcePath { get; init; }
        /// <summary>Ancestors, nearest-last (root … parent), for the breadcrumb.</summary>
        public IReadOnlyList<(int Id, string Name)> Ancestors { get; init; } = Array.Empty<(int, string)>();
        public IReadOnlyList<CollectionChild> Children { get; init; } = Array.Empty<CollectionChild>();
        public IReadOnlyList<CollectionItem> Items { get; init; } = Array.Empty<CollectionItem>();
        public IReadOnlyList<CollectionFile> Files { get; init; } = Array.Empty<CollectionFile>();
        public IReadOnlyList<AttributeBase> Metadata { get; init; } = Array.Empty<AttributeBase>();
        public string? CoverPath { get; init; }
    }

    /// <summary>
    /// Read access to the structural <see cref="Collection"/> tree for browsing (collection_plan.md §9.4),
    /// mirroring <c>LibraryService</c>: <see cref="GetRootsAsync"/> for the landing list and
    /// <see cref="GetAsync"/> for one collection's children/items/files/metadata. Direct LINQ over the
    /// context, consistent with the other services.
    /// </summary>
    public class CollectionService
    {
        private readonly DatabaseContext db;

        public CollectionService(DatabaseContext db)
        {
            this.db = db;
        }

        /// <summary>Root collections (no parent) for the Collections landing page.</summary>
        public async Task<IReadOnlyList<CollectionChild>> GetRootsAsync()
        {
            var roots = await db.Collections
                .Where(c => c.ParentCollectionId == null)
                .Select(c => new { c.Id, c.SourcePath, c.Kind })
                .ToListAsync();

            return await ToChildrenAsync(roots.Select(r => (r.Id, r.SourcePath, r.Kind)));
        }

        /// <summary>Full detail for one collection, or null if it doesn't exist.</summary>
        public async Task<CollectionDetails?> GetAsync(int id)
        {
            var col = await db.Collections
                .Where(c => c.Id == id)
                .Select(c => new { c.Id, c.SourcePath, c.Kind, c.ParentCollectionId })
                .FirstOrDefaultAsync();
            if (col is null)
                return null;

            // Ancestor chain (nearest-last) for the breadcrumb.
            var ancestors = new List<(int Id, string Name)>();
            int? cursor = col.ParentCollectionId;
            var guard = 0;
            while (cursor is int pid && guard++ < 64)
            {
                var parent = await db.Collections
                    .Where(c => c.Id == pid)
                    .Select(c => new { c.Id, c.SourcePath, c.ParentCollectionId })
                    .FirstOrDefaultAsync();
                if (parent is null)
                    break;
                ancestors.Add((parent.Id, NameOf(parent.SourcePath)));
                cursor = parent.ParentCollectionId;
            }
            ancestors.Reverse();

            var childRows = await db.Collections
                .Where(c => c.ParentCollectionId == id)
                .Select(c => new { c.Id, c.SourcePath, c.Kind })
                .ToListAsync();
            var children = await ToChildrenAsync(childRows.Select(r => (r.Id, r.SourcePath, r.Kind)));

            // Items directly in this collection, with their primary file (for the Item Viewer link + cover).
            var itemIds = await db.Items.Where(i => i.ParentCollectionId == id).Select(i => i.Id).ToListAsync();
            var primaries = await db.IndexedFiles
                .Where(f => f.ItemId != null && itemIds.Contains(f.ItemId!.Value) && f.Role == FileRole.Primary && f.Exists)
                .Select(f => new { ItemId = f.ItemId!.Value, f.Path })
                .ToListAsync();
            var companionsByItem = (await db.IndexedFiles
                .Where(f => f.ItemId != null && itemIds.Contains(f.ItemId!.Value) && f.Role == FileRole.Companion && f.Exists)
                .Select(f => new { ItemId = f.ItemId!.Value, f.Path })
                .ToListAsync())
                .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.Select(x => x.Path).ToList());

            var items = primaries
                .Select(p => new CollectionItem(
                    p.ItemId,
                    NameOf(p.Path),
                    p.Path,
                    Library.Sidecars.IsImage(NameOf(p.Path))
                        ? p.Path
                        : BestCover(companionsByItem.TryGetValue(p.ItemId, out var c) ? c : Enumerable.Empty<string>())))
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Files owned directly by the collection (art/nfo).
            var files = (await db.IndexedFiles
                .Where(f => f.CollectionId == id && f.Exists)
                .Select(f => new { f.Path, f.Role })
                .ToListAsync())
                .Select(f => new CollectionFile(f.Path, NameOf(f.Path), f.Role))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var metadata = await LoadCollectionAttributesAsync(id);

            return new CollectionDetails
            {
                Id = col.Id,
                Name = NameOf(col.SourcePath),
                Kind = col.Kind,
                SourcePath = col.SourcePath,
                Ancestors = ancestors,
                Children = children,
                Items = items,
                Files = files,
                Metadata = metadata,
                CoverPath = BestCover(files.Select(f => f.Path)),
            };
        }

        /// <summary>
        /// The collection context for an Item (collection_plan.md §9.2): the "Part of:" breadcrumb up the
        /// parent chain (root … nearest), and the nearest ancestor collection cover (so an episode with no
        /// thumbnail can fall back to its season/show poster). Empty when the item is in no collection.
        /// </summary>
        public async Task<(IReadOnlyList<(int Id, string Name)> Crumbs, string? Cover)> GetItemContextAsync(int itemId)
        {
            int? cursor = await db.Items.Where(i => i.Id == itemId)
                .Select(i => i.ParentCollectionId).FirstOrDefaultAsync();

            var chain = new List<(int Id, string Name)>();
            string? cover = null;
            var guard = 0;
            while (cursor is int cid && guard++ < 64)
            {
                var c = await db.Collections.Where(x => x.Id == cid)
                    .Select(x => new { x.Id, x.SourcePath, x.ParentCollectionId }).FirstOrDefaultAsync();
                if (c is null)
                    break;
                chain.Add((c.Id, NameOf(c.SourcePath)));
                if (cover is null)
                {
                    var art = await db.IndexedFiles.Where(f => f.CollectionId == c.Id && f.Exists)
                        .Select(f => f.Path).ToListAsync();
                    cover = BestCover(art);
                }
                cursor = c.ParentCollectionId;
            }
            chain.Reverse();
            return (chain, cover);
        }

        private async Task<IReadOnlyList<CollectionChild>> ToChildrenAsync(
            IEnumerable<(int Id, string? SourcePath, CollectionKind Kind)> rows)
        {
            var list = rows.ToList();
            var ids = list.Select(r => r.Id).ToList();

            var itemCounts = (await db.Items.Where(i => i.ParentCollectionId != null && ids.Contains(i.ParentCollectionId!.Value))
                .GroupBy(i => i.ParentCollectionId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Key, x => x.Count);
            var childCounts = (await db.Collections.Where(c => c.ParentCollectionId != null && ids.Contains(c.ParentCollectionId!.Value))
                .GroupBy(c => c.ParentCollectionId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Key, x => x.Count);

            return list
                .Select(r => new CollectionChild(r.Id, NameOf(r.SourcePath), r.Kind,
                    itemCounts.GetValueOrDefault(r.Id), childCounts.GetValueOrDefault(r.Id)))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<IReadOnlyList<AttributeBase>> LoadCollectionAttributesAsync(int id)
        {
            var result = new List<AttributeBase>();
            result.AddRange(await db.TextAttributes.Include(a => a.AttributeDefinition).Where(a => a.CollectionId == id).ToListAsync());
            result.AddRange(await db.IntegerAttributes.Include(a => a.AttributeDefinition).Where(a => a.CollectionId == id).ToListAsync());
            result.AddRange(await db.FloatAttributes.Include(a => a.AttributeDefinition).Where(a => a.CollectionId == id).ToListAsync());
            result.AddRange(await db.DateAttributes.Include(a => a.AttributeDefinition).Where(a => a.CollectionId == id).ToListAsync());
            result.AddRange(await db.BlobAttributes.Include(a => a.AttributeDefinition).Where(a => a.CollectionId == id).ToListAsync());
            return result;
        }

        /// <summary>Best cover image among a set of paths (cover &gt; folder &gt; poster &gt; … &gt; first).</summary>
        internal static string? BestCover(IEnumerable<string> paths)
        {
            string[] preference = { "cover", "folder", "front", "poster", "albumart", "thumb", "logo", "backdrop", "fanart" };
            return paths
                .Where(p => Library.Sidecars.IsImage(NameOf(p)))
                .OrderBy(p =>
                {
                    int i = Array.IndexOf(preference, System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant());
                    return i < 0 ? int.MaxValue : i;
                })
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>The display name of a collection — the last path segment of its source folder/archive.</summary>
        internal static string NameOf(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return "(collection)";
            int slash = path.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
        }
    }
}
