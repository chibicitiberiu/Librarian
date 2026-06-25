using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Librarian.Data;
using Librarian.DB;
using Librarian.Library;
using Librarian.Model;
using Librarian.Utils;
using Microsoft.EntityFrameworkCore;

namespace Librarian.Services
{
    /// <summary>One virtual folder at a drill level: a distinct attribute value plus how many
    /// files sit under it.</summary>
    public record LibraryFolderEntry(string Value, int Count);

    /// <summary>A leaf file in a category listing. Drawn from the index, so no disk access.</summary>
    public record LibraryFileEntry(int FileId, string Path, long? Size, DateTimeOffset Modified);

    /// <summary>The result of browsing a category at a given drill depth: either the virtual
    /// folders for the next level, or (at the deepest level) the matching files.</summary>
    public class LibraryListing
    {
        public bool IsLeaf { get; init; }
        public IReadOnlyList<LibraryFolderEntry> Folders { get; init; } = Array.Empty<LibraryFolderEntry>();
        public IReadOnlyList<LibraryFileEntry> Files { get; init; } = Array.Empty<LibraryFileEntry>();
    }

    /// <summary>
    /// Computes faceted ("category") listings live from the typed attribute tables. A category is
    /// <c>(filter) + (ordered drill-down path) + (leaf sort)</c>; browsing it level by level mirrors
    /// browsing a folder tree (see <see cref="LibraryCategory"/>). Read-only for now — no
    /// materialization (revisit only if real-library testing shows it's needed; plan Phase 6b).
    /// </summary>
    public class LibraryService
    {
        private readonly DatabaseContext db;

        // attribute definition id -> its storage type. Built once from the vocabulary.
        private static readonly IReadOnlyDictionary<int, AttributeType> AttributeTypes =
            Datasets.GetMetadataAttributes().ToDictionary(d => d.Id, d => d.Type);

        public LibraryService(DatabaseContext db)
        {
            this.db = db;
        }

        /// <summary>Browse a category view: <paramref name="filter"/> selects the files,
        /// <paramref name="path"/> is the view's drill-down, <paramref name="selected"/> are the
        /// values chosen so far. Returns the next level's folders, or (at the leaf) the files.</summary>
        public LibraryListing GetListing(CategoryFilter filter, IReadOnlyList<DrillLevel> path,
                                         int? leafSortAttributeId, IReadOnlyList<string> selected)
        {
            // Candidate files = filter ∩ primaries ∩ (chosen value at each already-selected level).
            // Sidecars/companions (an .opf, cover art, …) are folded into their primary and never
            // browsed directly (plan.md).
            var candidates = FilterFileIds(filter).Distinct().ToHashSet();
            candidates.IntersectWith(PrimaryFileIds());
            for (int i = 0; i < selected.Count && i < path.Count; i++)
            {
                var matching = MatchingFileIds(path[i], selected[i], candidates);
                candidates.IntersectWith(matching);
            }

            int depth = Math.Min(selected.Count, path.Count);
            var candidateList = candidates.ToList();

            // Deeper levels remain: list the virtual folders for the next drill level.
            if (depth < path.Count)
            {
                var level = path[depth];

                // Derived media-type level: folders are the reconciled type labels, not attribute values.
                if (level.Kind == DrillKind.MediaType)
                {
                    var typeFolders = MediaTypeLabels(candidateList)
                        .GroupBy(kv => kv.Value)
                        .Select(g => new LibraryFolderEntry(g.Key, g.Select(kv => kv.Key).Distinct().Count()))
                        .OrderBy(f => f.Value, NaturalComparer.Instance)
                        .ToList();
                    return new LibraryListing { IsLeaf = false, Folders = typeFolders };
                }

                // Date-derived level: folders are years (newest first) or months (chronological).
                if (level.Kind is DrillKind.DateYear or DrillKind.DateMonth)
                {
                    var dates = DateValues(candidateList, level.AttributeDefinitionId);
                    var dateFolders = level.Kind == DrillKind.DateYear
                        ? dates.GroupBy(kv => kv.Value.Year)
                               .OrderByDescending(g => g.Key)
                               .Select(g => new LibraryFolderEntry(g.Key.ToString(CultureInfo.InvariantCulture), g.Count()))
                        : dates.GroupBy(kv => kv.Value.Month)
                               .OrderBy(g => g.Key)
                               .Select(g => new LibraryFolderEntry(MonthName(g.Key), g.Count()));
                    return new LibraryListing { IsLeaf = false, Folders = dateFolders.ToList() };
                }

                bool numeric = TypeOf(level.AttributeDefinitionId) == AttributeType.Integer;
                var pairs = LevelPairs(level, candidateList);

                var folders = pairs
                    .GroupBy(p => p.Value)
                    .Select(g => new
                    {
                        Entry = new LibraryFolderEntry(g.Key, g.Select(p => p.FileId).Distinct().Count()),
                        NumKey = g.First().NumKey,
                    });

                folders = numeric
                    ? folders.OrderBy(f => f.NumKey ?? long.MaxValue).ThenBy(f => f.Entry.Value, NaturalComparer.Instance)
                    : folders.OrderBy(f => f.Entry.Value, NaturalComparer.Instance);

                return new LibraryListing { IsLeaf = false, Folders = folders.Select(f => f.Entry).ToList() };
            }

            // Deepest level: list the matching files.
            return new LibraryListing { IsLeaf = true, Files = LeafFiles(leafSortAttributeId, candidateList) };
        }

        /// <summary>
        /// How many entries a view shows at its top level — used for the category's view list. For a
        /// drill view that's the number of distinct first-level values; for a flat view, the file count.
        /// </summary>
        public int CountView(CategoryFilter filter, CategoryView view)
        {
            var candidateSet = FilterFileIds(filter).Distinct().ToHashSet();
            candidateSet.IntersectWith(PrimaryFileIds());
            var candidates = candidateSet.ToList();

            if (view.Path.Count == 0)
                return candidates.Count;

            var first = view.Path[0];
            if (first.Kind == DrillKind.MediaType)
                return MediaTypeLabels(candidates).Values.Distinct().Count();
            if (first.Kind is DrillKind.DateYear or DrillKind.DateMonth)
            {
                var dates = DateValues(candidates, first.AttributeDefinitionId).Values;
                return (first.Kind == DrillKind.DateYear ? dates.Select(d => d.Year) : dates.Select(d => d.Month)).Distinct().Count();
            }

            return LevelPairs(first, candidates).Select(p => p.Value).Distinct().Count();
        }

        /// <summary>Ids of files that browse on their own (not folded into another file's work).</summary>
        private HashSet<int> PrimaryFileIds() =>
            db.IndexedFiles.Where(f => f.Exists && f.Role == FileRole.Primary).Select(f => f.Id).ToHashSet();

        /// <summary>File ids matching a chosen folder value at a level — attribute value or derived type.</summary>
        private ISet<int> MatchingFileIds(DrillLevel level, string value, IReadOnlyCollection<int> candidates)
        {
            if (level.Kind == DrillKind.MediaType)
                return MediaTypeLabels(candidates.ToList())
                    .Where(kv => kv.Value == value)
                    .Select(kv => kv.Key)
                    .ToHashSet();

            if (level.Kind is DrillKind.DateYear or DrillKind.DateMonth)
            {
                var dates = DateValues(candidates.ToList(), level.AttributeDefinitionId);
                return dates
                    .Where(kv => (level.Kind == DrillKind.DateYear
                                    ? kv.Value.Year.ToString(CultureInfo.InvariantCulture)
                                    : MonthName(kv.Value.Month)) == value)
                    .Select(kv => kv.Key)
                    .ToHashSet();
            }

            return FileIdsWithLevelValue(level, value).ToHashSet();
        }

        /// <summary>Each candidate file's date for a given date attribute (earliest, if several).</summary>
        private Dictionary<int, DateTimeOffset> DateValues(IReadOnlyList<int> candidateIds, int dateDefId)
        {
            if (candidateIds.Count == 0)
                return new Dictionary<int, DateTimeOffset>();

            return db.DateAttributes
                .Where(a => a.AttributeDefinitionId == dateDefId && a.SubResourceId == null
                            && a.FileId != null && candidateIds.Contains(a.FileId.Value))
                .Select(a => new { FileId = a.FileId!.Value, a.Value })
                .ToList()
                .GroupBy(a => a.FileId)
                .ToDictionary(g => g.Key, g => g.Min(a => a.Value));
        }

        private static string MonthName(int month) =>
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

        /// <summary>Resolves each candidate file's derived media-type label from its file-level MIME
        /// signals (file-service MIME reconciled with Tika's content type). Files with no MIME at all
        /// are omitted — they simply don't appear under a "By Type" view.</summary>
        private Dictionary<int, string> MediaTypeLabels(IReadOnlyList<int> candidateIds)
        {
            if (candidateIds.Count == 0)
                return new Dictionary<int, string>();

            int mimeId = Model.MetadataAttributes.FileAttributes.MimeType;
            int contentTypeId = Model.MetadataAttributes.General.ContentType;

            var rows = db.TextAttributes
                .Where(a => (a.AttributeDefinitionId == mimeId || a.AttributeDefinitionId == contentTypeId)
                            && a.SubResourceId == null
                            && a.FileId != null && candidateIds.Contains(a.FileId.Value))
                .Select(a => new { FileId = a.FileId!.Value, a.AttributeDefinitionId, a.Value })
                .ToList();

            var result = new Dictionary<int, string>();
            foreach (var g in rows.GroupBy(r => r.FileId))
            {
                string? fileMime = g.FirstOrDefault(x => x.AttributeDefinitionId == mimeId)?.Value;
                string? tika = g.FirstOrDefault(x => x.AttributeDefinitionId == contentTypeId)?.Value;
                result[g.Key] = MediaType.Resolve(fileMime, tika).Label;
            }
            return result;
        }

        #region Filtering

        private IQueryable<int> FilterFileIds(CategoryFilter filter)
        {
            switch (filter.Kind)
            {
                case CategoryFilterKind.None:
                    return db.IndexedFiles.Where(f => f.Exists).Select(f => f.Id);

                case CategoryFilterKind.MimePrefix:
                    IQueryable<int>? q = null;
                    foreach (var prefix in filter.MimePrefixes)
                    {
                        var part = db.TextAttributes
                            .Where(a => a.AttributeDefinitionId == Model.MetadataAttributes.FileAttributes.MimeType
                                        && a.FileId != null && a.SubResourceId == null
                                        && EF.Functions.Like(a.Value, prefix + "%"))
                            .Select(a => a.FileId!.Value);
                        q = q == null ? part : q.Union(part);
                    }
                    return q ?? Empty();

                case CategoryFilterKind.HasAttribute:
                    return AttributeFileIds(filter.AttributeDefinitionId);

                case CategoryFilterKind.HasGroup:
                    return GroupFileIds(filter.Group!);

                default:
                    return Empty();
            }
        }

        private IQueryable<int> Empty() => db.IndexedFiles.Where(_ => false).Select(f => f.Id);

        /// <summary>File ids that have any value for the given attribute, from whichever typed
        /// table stores that attribute's type.</summary>
        private IQueryable<int> AttributeFileIds(int defId)
        {
            return TypeOf(defId) switch
            {
                AttributeType.Text or AttributeType.BigText or AttributeType.FormattedText =>
                    db.TextAttributes.Where(a => a.AttributeDefinitionId == defId && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                AttributeType.Integer =>
                    db.IntegerAttributes.Where(a => a.AttributeDefinitionId == defId && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                AttributeType.Float or AttributeType.TimeSpan =>
                    db.FloatAttributes.Where(a => a.AttributeDefinitionId == defId && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                AttributeType.Date =>
                    db.DateAttributes.Where(a => a.AttributeDefinitionId == defId && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                AttributeType.Blob =>
                    db.BlobAttributes.Where(a => a.AttributeDefinitionId == defId && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                _ => Empty(),
            };
        }

        /// <summary>File ids that have any attribute belonging to a vocabulary group.</summary>
        private IQueryable<int> GroupFileIds(string group)
        {
            var parts = new[]
            {
                db.TextAttributes.Where(a => a.AttributeDefinition.Group == group && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                db.IntegerAttributes.Where(a => a.AttributeDefinition.Group == group && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                db.FloatAttributes.Where(a => a.AttributeDefinition.Group == group && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
                db.DateAttributes.Where(a => a.AttributeDefinition.Group == group && a.FileId != null && a.SubResourceId == null).Select(a => a.FileId!.Value),
            };
            return parts.Aggregate((a, b) => a.Union(b));
        }

        /// <summary>File ids whose value for a drill level equals the chosen folder value.</summary>
        private IQueryable<int> FileIdsWithLevelValue(DrillLevel level, string value)
        {
            switch (TypeOf(level.AttributeDefinitionId))
            {
                case AttributeType.Text:
                case AttributeType.BigText:
                case AttributeType.FormattedText:
                    return db.TextAttributes
                        .Where(a => a.AttributeDefinitionId == level.AttributeDefinitionId && a.Value == value && a.FileId != null && a.SubResourceId == null)
                        .Select(a => a.FileId!.Value);

                case AttributeType.Integer when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n):
                    return db.IntegerAttributes
                        .Where(a => a.AttributeDefinitionId == level.AttributeDefinitionId && a.Value == n && a.FileId != null && a.SubResourceId == null)
                        .Select(a => a.FileId!.Value);

                default:
                    return Empty();
            }
        }

        #endregion

        #region Value extraction

        private record LevelPair(int FileId, string Value, long? NumKey);

        /// <summary>(file, value) pairs for a drill level, restricted to the candidate files.</summary>
        private List<LevelPair> LevelPairs(DrillLevel level, IReadOnlyList<int> candidateIds)
        {
            if (candidateIds.Count == 0)
                return new List<LevelPair>();

            switch (TypeOf(level.AttributeDefinitionId))
            {
                case AttributeType.Text:
                case AttributeType.BigText:
                case AttributeType.FormattedText:
                    return db.TextAttributes
                        .Where(a => a.AttributeDefinitionId == level.AttributeDefinitionId
                                    && a.FileId != null && a.SubResourceId == null && candidateIds.Contains(a.FileId.Value))
                        .Select(a => new { FileId = a.FileId!.Value, a.Value })
                        .ToList()
                        .Select(a => new LevelPair(a.FileId, a.Value, null))
                        .ToList();

                case AttributeType.Integer:
                    return db.IntegerAttributes
                        .Where(a => a.AttributeDefinitionId == level.AttributeDefinitionId
                                    && a.FileId != null && a.SubResourceId == null && candidateIds.Contains(a.FileId.Value))
                        .Select(a => new { FileId = a.FileId!.Value, a.Value })
                        .ToList()
                        .Select(a => new LevelPair(a.FileId, a.Value.ToString(CultureInfo.InvariantCulture), a.Value))
                        .ToList();

                default:
                    return new List<LevelPair>();
            }
        }

        private List<LibraryFileEntry> LeafFiles(int? leafSortAttributeId, IReadOnlyList<int> candidateIds)
        {
            if (candidateIds.Count == 0)
                return new List<LibraryFileEntry>();

            var files = db.IndexedFiles
                .Where(f => f.Exists && candidateIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Path, f.Size, f.Modified })
                .ToList();

            // Optional leaf sort by a chosen attribute (e.g. Track), falling back to natural name.
            Dictionary<int, LevelPair>? sortKeys = null;
            if (leafSortAttributeId is int sortId)
            {
                sortKeys = LevelPairs(new DrillLevel(sortId, ""), candidateIds)
                    .GroupBy(p => p.FileId)
                    .ToDictionary(g => g.Key, g => g.First());
            }

            bool numericSort = leafSortAttributeId is int id && TypeOf(id) == AttributeType.Integer;

            IEnumerable<LibraryFileEntry> ordered = files
                .Select(f => new LibraryFileEntry(f.Id, f.Path, f.Size, f.Modified));

            if (sortKeys != null && numericSort)
            {
                ordered = ordered.OrderBy(f => sortKeys.TryGetValue(f.FileId, out var k) ? k.NumKey ?? long.MaxValue : long.MaxValue)
                                 .ThenBy(NameOf, NaturalComparer.Instance);
            }
            else if (sortKeys != null)
            {
                ordered = ordered.OrderBy(f => sortKeys.TryGetValue(f.FileId, out var k) ? k.Value : "￿", NaturalComparer.Instance)
                                 .ThenBy(NameOf, NaturalComparer.Instance);
            }
            else
            {
                ordered = ordered.OrderBy(NameOf, NaturalComparer.Instance);
            }

            return ordered.ToList();
        }

        private static string NameOf(LibraryFileEntry f)
        {
            int slash = f.Path.LastIndexOf('/');
            return slash >= 0 && slash < f.Path.Length - 1 ? f.Path[(slash + 1)..] : f.Path;
        }

        #endregion

        private static AttributeType TypeOf(int defId) =>
            AttributeTypes.TryGetValue(defId, out var t) ? t : AttributeType.Text;
    }
}
