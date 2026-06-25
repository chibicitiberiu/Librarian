using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Librarian.DB;
using Librarian.Library;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Attr = Librarian.Model.MetadataAttributes;

namespace Librarian.Services
{
    public record AssociationResult(int Items, int Sidecars, int Companions, int Promotions);

    /// <summary>
    /// Groups indexed files into <see cref="Item"/>s — the catalogued unit (plan.md Standing decisions) — and
    /// promotes each Item's sidecar metadata onto its primary file. Per folder it decides the bundle
    /// shape: an app/game install (an executable + resources) collapses to a single Item whose primary
    /// is the chosen app exe and whose every other file (extra exes, DLLs, manuals, README) is a
    /// companion; a book folder (one content file + .opf/cover) is a single Item; an album or a folder
    /// of loose files yields one Item per content file (tracks stay individual — §browsing decision).
    ///
    /// It operates only on the existing index (re-runnable, like renormalize) and is idempotent: each
    /// run resets the heuristic's own assignments and rebuilds. User corrections (<see cref="RoleSource.Manual"/>)
    /// are left untouched (plan.md Standing decisions) — the override machinery exists; the editing UX is M6.
    /// </summary>
    public class ItemAssociationService
    {
        /// <summary>Provider id stamped on promoted (copied) attributes. Public so the indexer can
        /// preserve these when it idempotently rebuilds a file's other canonical attributes.</summary>
        public static readonly Guid PromotionProvider = new("d3f0a1c2-7b64-4e90-a1f2-9c0b5e3a77d1");

        private static readonly int[] PromotedTextDefs =
        {
            Attr.General.WrittenBy, Attr.General.Tag, Attr.General.Description,
            Attr.General.Publisher, Attr.General.Language,
        };
        private static readonly int[] PromotedDateDefs = { Attr.General.DateReleased };

        private readonly DatabaseContext db;
        private readonly SearchVectorService searchVectors;
        private readonly MetadataService metadataService;
        private readonly ILogger<ItemAssociationService> logger;

        public ItemAssociationService(DatabaseContext db, SearchVectorService searchVectors,
                                      MetadataService metadataService, ILogger<ItemAssociationService> logger)
        {
            this.db = db;
            this.searchVectors = searchVectors;
            this.metadataService = metadataService;
            this.logger = logger;
        }

        public async Task<AssociationResult> AssociateAllAsync()
        {
            await ResetAsync();

            // Only the heuristic's own (Auto) files are re-associated; manual corrections are preserved.
            var files = await db.IndexedFiles
                .Where(f => f.Exists && f.RoleSource == RoleSource.Auto)
                .ToListAsync();

            // Directory entries are containers, not catalogue items — leave them out of Items entirely.
            // A path that is some other file's parent is a directory.
            var dirPaths = files.Select(f => ParentDir(f.Path)).Where(d => d.Length > 0).ToHashSet();

            // An archive whose entries are virtual files is itself a container, not a catalogue item
            // (collection_plan.md §6): exclude it like a directory. Its entries participate as the files
            // within it, grouped by their internal folder (their "archive.zip!/dir" parent path).
            var containerIds = files.Where(f => f.ParentFileId is int)
                                    .Select(f => f.ParentFileId!.Value).ToHashSet();

            var realFiles = files.Where(f => !dirPaths.Contains(f.Path) && !containerIds.Contains(f.Id)).ToList();
            var byFolder = realFiles.GroupBy(f => ParentDir(f.Path)).ToList();

            // An app/game install folder = an executable + resource files (DLLs/data) together. A
            // catch-all folder that merely holds a loose installer is NOT one. Such a bundle owns its
            // whole subtree (manuals/trainers in subfolders fold in), so only the outermost qualifies.
            var appFolders = byFolder
                .Where(g => g.Any(f => IsContentExe(f)) && g.Any(f => Sidecars.Classify(NameOf(f.Path)) == SidecarKind.CompanionResource))
                .Select(g => g.Key)
                .ToHashSet();
            var roots = appFolders.Where(d => !AncestorDirs(d).Any(appFolders.Contains)).ToHashSet();

            var promotions = new List<(IndexedFile Sidecar, IndexedFile Primary)>();
            int items = 0, sidecars = 0, companions = 0;

            var byRoot = new Dictionary<string, List<IndexedFile>>();
            var looseFolders = new List<IGrouping<string, IndexedFile>>();
            foreach (var folder in byFolder)
            {
                string? root = NearestRoot(folder.Key, roots);
                if (root is not null)
                    (byRoot.TryGetValue(root, out var list) ? list : byRoot[root] = new()).AddRange(folder);
                else
                    looseFolders.Add(folder);
            }

            foreach (var (rootDir, bundleFiles) in byRoot)
            {
                var r = ProcessAppBundle(rootDir, bundleFiles, promotions);
                items += r.Items; sidecars += r.Sidecars; companions += r.Companions;
            }
            foreach (var folder in looseFolders)
            {
                var r = AssociateFolder(folder.ToList(), promotions);
                items += r.Items; sidecars += r.Sidecars; companions += r.Companions;
            }
            await db.SaveChangesAsync();

            // Promote each Item's sidecar metadata onto its primary.
            int promoted = 0;
            var touched = new HashSet<int>();
            foreach (var (sidecar, primary) in promotions)
            {
                int produced = Sidecars.Classify(NameOf(sidecar.Path)) switch
                {
                    SidecarKind.Opf => await PromoteAttributesAsync(sidecar.Id, primary.Id),
                    SidecarKind.Lrc => await PromoteContentAsync(sidecar.Id, primary.Id, Attr.Audio.Lyrics),
                    SidecarKind.Nfo => await PromoteContentAsync(sidecar.Id, primary.Id, Attr.General.Comment),
                    _ => 0,
                };
                if (produced > 0) { promoted += produced; touched.Add(primary.Id); }
            }
            await db.SaveChangesAsync();

            foreach (int id in touched)
                await searchVectors.UpdateFileVectorsAsync(id);

            // Structural layer: fold the directory tree into recursive Collections (Show ⊃ Season ⊃
            // Episode, …) on top of the Items just built (collection_plan.md §6). Additive — it never
            // changes Item membership, only parents Items/Collections and re-owns collection-level art/nfo.
            int collections = await BuildStructuralCollectionsAsync();

            logger.LogInformation("Item association: {Items} items, {Sidecars} sidecars, {Companions} companions, {Promotions} promoted, {Collections} collections.",
                items, sidecars, companions, promoted, collections);
            return new AssociationResult(items, sidecars, companions, promoted);
        }

        /// <summary>Builds the Item(s) for one non-bundle folder (app/game bundles go through
        /// <see cref="ProcessAppBundle"/>). A single content file (a book) is one Item; multiple
        /// content files (an album's tracks, loose files) become one Item each.</summary>
        private (int Items, int Sidecars, int Companions) AssociateFolder(
            List<IndexedFile> folderFiles, List<(IndexedFile, IndexedFile)> promotions)
        {
            var classified = folderFiles
                .Select(f => (file: f, name: NameOf(f.Path), kind: Sidecars.Classify(NameOf(f.Path))))
                .ToList();

            bool isBundle = classified.Any(c => c.kind == SidecarKind.Content
                                                && (Sidecars.IsAudio(c.name) || Sidecars.IsExecutable(c.name)));

            // Loose images inside an audio/app folder are art/resources, not standalone photos (M3b).
            bool IsCompanion(SidecarKind kind, string name) =>
                kind is SidecarKind.CompanionArt or SidecarKind.CompanionResource
                || (isBundle && kind == SidecarKind.Content && Sidecars.IsImage(name));

            var realContent = classified
                .Where(c => c.kind == SidecarKind.Content && !IsCompanion(c.kind, c.name))
                .Select(c => c.file)
                .ToList();

            if (realContent.Count == 1)
                return SingleItem(classified, realContent[0], IsCompanion, promotions);

            return PerContentItems(classified, realContent, IsCompanion, promotions);
        }

        /// <summary>An app/game bundle's whole subtree is one Item: the primary is the chosen app exe
        /// in the root folder; every other file — extra exes, DLLs, data, manuals, trainers — is a
        /// companion (or a metadata sidecar that promotes onto the primary).</summary>
        private (int Items, int Sidecars, int Companions) ProcessAppBundle(
            string rootDir, List<IndexedFile> bundleFiles, List<(IndexedFile, IndexedFile)> promotions)
        {
            var rootExes = bundleFiles
                .Where(f => ParentDir(f.Path) == rootDir && IsContentExe(f))
                .ToList();
            var primary = ChoosePrimaryExe(rootExes, LastSegment(rootDir))
                          ?? bundleFiles.FirstOrDefault(f => Sidecars.Classify(NameOf(f.Path)) == SidecarKind.Content);
            if (primary is null)
                return (0, 0, 0);

            var item = new Item { RoleSource = RoleSource.Auto };
            db.Items.Add(item);

            int sidecars = 0, companions = 0;
            foreach (var file in bundleFiles)
            {
                file.Item = item;
                file.RoleSource = RoleSource.Auto;

                if (file == primary)
                {
                    file.Role = FileRole.Primary;
                }
                else if (Sidecars.Classify(NameOf(file.Path)) is SidecarKind.Opf or SidecarKind.Nfo or SidecarKind.Lrc)
                {
                    file.Role = FileRole.Sidecar;
                    promotions.Add((file, primary));
                    sidecars++;
                }
                else
                {
                    file.Role = FileRole.Companion;
                    companions++;
                }
            }
            return (1, sidecars, companions);
        }

        private static bool IsContentExe(IndexedFile f) =>
            Sidecars.Classify(NameOf(f.Path)) == SidecarKind.Content && Sidecars.IsExecutable(NameOf(f.Path));

        /// <summary>Whole folder is one Item owned by <paramref name="primary"/>.</summary>
        private (int, int, int) SingleItem(
            List<(IndexedFile file, string name, SidecarKind kind)> classified,
            IndexedFile primary,
            Func<SidecarKind, string, bool> isCompanion,
            List<(IndexedFile, IndexedFile)> promotions)
        {
            var item = new Item { RoleSource = RoleSource.Auto };
            db.Items.Add(item);

            int sidecars = 0, companions = 0;
            foreach (var (file, name, kind) in classified)
            {
                file.Item = item;
                file.RoleSource = RoleSource.Auto;

                if (file == primary)
                {
                    file.Role = FileRole.Primary;
                }
                else if (!isCompanion(kind, name) && kind is SidecarKind.Opf or SidecarKind.Nfo or SidecarKind.Lrc)
                {
                    file.Role = FileRole.Sidecar;
                    promotions.Add((file, primary));
                    sidecars++;
                }
                else
                {
                    file.Role = FileRole.Companion;
                    companions++;
                }
            }
            return (1, sidecars, companions);
        }

        /// <summary>Each content file is its own Item; sidecars bind to a same-named content, art is
        /// a shared orphan companion (no single owner).</summary>
        private (int, int, int) PerContentItems(
            List<(IndexedFile file, string name, SidecarKind kind)> classified,
            List<IndexedFile> realContent,
            Func<SidecarKind, string, bool> isCompanion,
            List<(IndexedFile, IndexedFile)> promotions)
        {
            var itemByContent = new Dictionary<IndexedFile, Item>();
            foreach (var content in realContent)
            {
                var item = new Item { RoleSource = RoleSource.Auto };
                db.Items.Add(item);
                content.Item = item;
                content.Role = FileRole.Primary;
                content.RoleSource = RoleSource.Auto;
                itemByContent[content] = item;
            }

            int sidecars = 0, companions = 0;
            foreach (var (file, name, kind) in classified)
            {
                if (itemByContent.ContainsKey(file))
                    continue;

                file.RoleSource = RoleSource.Auto;

                // Lyrics bind to the track of the same name; other sidecars have no unambiguous owner.
                if (kind == SidecarKind.Lrc)
                {
                    var track = realContent.FirstOrDefault(c => Sidecars.Stem(NameOf(c.Path)) == Sidecars.Stem(name));
                    file.Role = FileRole.Sidecar;
                    sidecars++;
                    if (track is not null)
                    {
                        file.Item = itemByContent[track];
                        promotions.Add((file, track));
                    }
                }
                else if (kind is SidecarKind.Opf or SidecarKind.Nfo)
                {
                    file.Role = FileRole.Sidecar; // orphan: ambiguous primary, no promotion
                    sidecars++;
                }
                else
                {
                    file.Role = FileRole.Companion; // shared art / resource, no single owner
                    companions++;
                }
            }
            return (realContent.Count, sidecars, companions);
        }

        /// <summary>Picks a bundle's primary executable: a folder-name match first, otherwise the
        /// largest non-auxiliary exe (skipping installers/archivers/redists).</summary>
        private static IndexedFile? ChoosePrimaryExe(List<IndexedFile> exes, string folderName)
        {
            if (exes.Count == 0)
                return null;
            if (exes.Count == 1)
                return exes[0];

            string folderKey = Sidecars.MatchKey(folderName);
            if (folderKey.Length > 0)
            {
                var match = exes.FirstOrDefault(e =>
                {
                    string k = Sidecars.MatchKey(Path.GetFileNameWithoutExtension(NameOf(e.Path)));
                    return k.Length > 0 && (folderKey == k || folderKey.Contains(k) || k.Contains(folderKey));
                });
                if (match is not null)
                    return match;
            }

            var candidates = exes.Where(e => !Sidecars.IsAuxiliaryExecutable(NameOf(e.Path))).ToList();
            if (candidates.Count == 0)
                candidates = exes;
            return candidates.OrderByDescending(e => e.Size ?? 0).ThenBy(e => NameOf(e.Path), StringComparer.Ordinal).First();
        }

        private async Task<int> PromoteContentAsync(int sidecarId, int primaryId, int definitionId)
        {
            string? content = await db.IndexedFileContents
                .Where(c => c.FileId == sidecarId).Select(c => c.Content).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            bool exists = await db.TextAttributes
                .AnyAsync(a => a.FileId == primaryId && a.AttributeDefinitionId == definitionId && a.SubResourceId == null);
            if (exists)
                return 0;

            db.TextAttributes.Add(new TextAttribute
            {
                FileId = primaryId,
                AttributeDefinitionId = definitionId,
                Value = content.Trim(),
                ProviderId = PromotionProvider.ToString(),
                ProviderAttributeId = $"sidecar:{sidecarId}",
                Editable = false,
            });
            return 1;
        }

        private async Task<int> PromoteAttributesAsync(int sidecarId, int primaryId)
        {
            int produced = 0;

            var texts = await db.TextAttributes
                .Where(a => a.FileId == sidecarId && a.SubResourceId == null && PromotedTextDefs.Contains(a.AttributeDefinitionId))
                .Select(a => new { a.AttributeDefinitionId, a.Value }).ToListAsync();

            var existingText = (await db.TextAttributes
                .Where(a => a.FileId == primaryId && a.SubResourceId == null && PromotedTextDefs.Contains(a.AttributeDefinitionId))
                .Select(a => new { a.AttributeDefinitionId, a.Value }).ToListAsync())
                .Select(a => (a.AttributeDefinitionId, a.Value)).ToHashSet();

            foreach (var t in texts)
            {
                if (!existingText.Add((t.AttributeDefinitionId, t.Value)))
                    continue;
                db.TextAttributes.Add(new TextAttribute
                {
                    FileId = primaryId,
                    AttributeDefinitionId = t.AttributeDefinitionId,
                    Value = t.Value,
                    ProviderId = PromotionProvider.ToString(),
                    ProviderAttributeId = $"opf:{sidecarId}",
                    Editable = false,
                });
                produced++;
            }

            var dates = await db.DateAttributes
                .Where(a => a.FileId == sidecarId && a.SubResourceId == null && PromotedDateDefs.Contains(a.AttributeDefinitionId))
                .Select(a => new { a.AttributeDefinitionId, a.Value }).ToListAsync();
            var existingDate = (await db.DateAttributes
                .Where(a => a.FileId == primaryId && a.SubResourceId == null && PromotedDateDefs.Contains(a.AttributeDefinitionId))
                .Select(a => a.AttributeDefinitionId).ToListAsync()).ToHashSet();

            foreach (var d in dates)
            {
                if (!existingDate.Add(d.AttributeDefinitionId))
                    continue;
                db.DateAttributes.Add(new DateAttribute
                {
                    FileId = primaryId,
                    AttributeDefinitionId = d.AttributeDefinitionId,
                    Value = d.Value,
                    ProviderId = PromotionProvider.ToString(),
                    ProviderAttributeId = $"opf:{sidecarId}",
                    Editable = false,
                });
                produced++;
            }

            return produced;
        }

        /// <summary>Undoes the heuristic's previous output: drops promoted attributes, resets every
        /// Auto file to a standalone Primary, and deletes Auto Items. Manual corrections survive.</summary>
        private async Task ResetAsync()
        {
            string promo = PromotionProvider.ToString();
            await db.TextAttributes.Where(a => a.ProviderId == promo).ExecuteDeleteAsync();
            await db.DateAttributes.Where(a => a.ProviderId == promo).ExecuteDeleteAsync();

            await db.IndexedFiles
                .Where(f => f.RoleSource == RoleSource.Auto && (f.Role != FileRole.Primary || f.ItemId != null))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.Role, FileRole.Primary)
                    .SetProperty(f => f.ItemId, (int?)null));

            await db.Items.Where(i => i.RoleSource == RoleSource.Auto).ExecuteDeleteAsync();

            // Tear down the heuristic's structural Collections too (collection_plan.md §6.4). Manual
            // collections survive and re-bind by SourcePath on the rebuild. Null every reference to an
            // Auto collection first (the self-ref FK is Restrict), then delete them.
            var autoColIds = await db.Collections
                .Where(c => c.RoleSource == RoleSource.Auto)
                .Select(c => c.Id).ToListAsync();
            if (autoColIds.Count > 0)
            {
                await db.Collections.Where(c => c.ParentCollectionId != null && autoColIds.Contains(c.ParentCollectionId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCollectionId, (int?)null));
                await db.Items.Where(i => i.ParentCollectionId != null && autoColIds.Contains(i.ParentCollectionId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.ParentCollectionId, (int?)null));
                await db.IndexedFiles.Where(f => f.CollectionId != null && autoColIds.Contains(f.CollectionId!.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.CollectionId, (int?)null));
                await db.Collections.Where(c => autoColIds.Contains(c.Id)).ExecuteDeleteAsync();
            }
            // Collection-scoped promoted attributes were already removed by the ProviderId == promo
            // deletes above (those are keyed only by provider, so they catch file- and collection-owned).
        }

        #region Structural collections (collection_plan.md §6)

        /// <summary>A folder/archive-subdir name like "Season 1", "Disc 2", "Vol. 3", "Specials" — the
        /// naming signal that a content folder is one structural level of a larger work.</summary>
        // No \b after the keyword: a level can be written with no separator ("S01") or with an underscore
        // ("Part_4"), neither of which is a word boundary. The trailing $ keeps it from matching prefixes.
        private static readonly System.Text.RegularExpressions.Regex StructuralName = new(
            @"^(season|series|saison|book|vol|volume|disc|disk|cd|part|pt|s)[\s._-]*\d+$|^(specials?|extras?|bonus)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool IsStructuralName(string name) => StructuralName.IsMatch(name.Trim());

        private static bool IsSeasonName(string name)
        {
            string n = name.Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(n, @"^(season|series|saison|s)[\s._-]*\d+$",
                       System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                   || n.Equals("specials", StringComparison.OrdinalIgnoreCase)
                   || n.Equals("special", StringComparison.OrdinalIgnoreCase)
                   || n.Equals("extras", StringComparison.OrdinalIgnoreCase);
        }

        private readonly record struct ResolvedNode(Collection? Collection, IReadOnlyList<int> ItemIds);

        /// <summary>
        /// Folds the real-folder tree into recursive Collections on top of the Items already built. A
        /// folder of folders (each holding content) becomes a Collection; season/disc/volume-named child
        /// folders become sub-Collections (a Show ⊃ Season ⊃ Episode hierarchy); collection-level art/nfo
        /// (poster, tvshow.nfo) is re-owned by the Collection and its nfo promoted. Archive subtrees are
        /// left to their flat Item grouping for now (their "!/"-paths are excluded here).
        /// </summary>
        private async Task<int> BuildStructuralCollectionsAsync()
        {
            // Tracked entities — we mutate navigation properties and let EF assign the FKs on save.
            var files = await db.IndexedFiles
                .Where(f => f.Exists && f.RoleSource == RoleSource.Auto && f.Source == FileSource.Filesystem)
                .ToListAsync();
            var autoItems = await db.Items.Where(i => i.RoleSource == RoleSource.Auto).ToListAsync();
            var itemById = autoItems.ToDictionary(i => i.Id);

            // folder → files directly in it; folder → item ids whose primary lives directly in it.
            var filesByFolder = new Dictionary<string, List<IndexedFile>>();
            var itemsByFolder = new Dictionary<string, List<int>>();
            foreach (var f in files)
            {
                string folder = ParentDir(f.Path);
                if (folder.Length == 0)
                    continue;
                (filesByFolder.TryGetValue(folder, out var fl) ? fl : filesByFolder[folder] = new()).Add(f);
                if (f.Role == FileRole.Primary && f.ItemId is int itemId && itemById.ContainsKey(itemId))
                    (itemsByFolder.TryGetValue(folder, out var il) ? il : itemsByFolder[folder] = new()).Add(itemId);
            }

            // The full folder set (including ancestor folders with no direct files), and each folder's
            // immediate children.
            var allFolders = new HashSet<string>();
            foreach (var folder in filesByFolder.Keys)
                for (string d = folder; d.Length > 0; d = ParentDir(d))
                    allFolders.Add(d);

            var childFolders = new Dictionary<string, List<string>>();
            foreach (var folder in allFolders)
            {
                string parent = ParentDir(folder);
                if (parent.Length > 0)
                    (childFolders.TryGetValue(parent, out var cl) ? cl : childFolders[parent] = new()).Add(folder);
            }

            // Manual collections re-bind by their source path; never recreate or repurpose them.
            var manualByPath = (await db.Collections
                    .Where(c => c.RoleSource == RoleSource.Manual && c.SourcePath != null)
                    .ToListAsync())
                .ToDictionary(c => c.SourcePath!, c => c);

            var created = new List<Collection>();
            var nfoPromotions = new List<(int FileId, Collection Collection)>();
            var memo = new Dictionary<string, ResolvedNode>();

            Collection GetOrCreate(string folder, CollectionKind kind)
            {
                if (manualByPath.TryGetValue(folder, out var pinned))
                    return pinned;
                var c = new Collection { SourcePath = folder, Kind = kind, RoleSource = RoleSource.Auto };
                db.Collections.Add(c);
                created.Add(c);
                return c;
            }

            void AttachArtNfo(string folder, Collection col)
            {
                if (!filesByFolder.TryGetValue(folder, out var folderFiles))
                    return;
                foreach (var file in folderFiles)
                {
                    var kind = Sidecars.Classify(NameOf(file.Path));
                    if (kind == SidecarKind.CompanionArt)
                    {
                        file.Item = null; file.ItemId = null;
                        file.Collection = col;
                        file.Role = FileRole.Companion;
                    }
                    else if (kind == SidecarKind.Nfo)
                    {
                        file.Item = null; file.ItemId = null;
                        file.Collection = col;
                        file.Role = FileRole.Sidecar;
                        nfoPromotions.Add((file.Id, col));
                    }
                }
            }

            ResolvedNode Resolve(string folder)
            {
                if (memo.TryGetValue(folder, out var cached))
                    return cached;
                // Guard the memo against the (impossible for a tree, but cheap) re-entrancy case.
                memo[folder] = new ResolvedNode(null, System.Array.Empty<int>());

                var kids = childFolders.TryGetValue(folder, out var k) ? k : new List<string>();
                var childRes = kids.ToDictionary(c => c, Resolve);
                var looseItems = itemsByFolder.TryGetValue(folder, out var li) ? li : new List<int>();

                int nonEmptyChildren = childRes.Count(kv => kv.Value.Collection != null || kv.Value.ItemIds.Count > 0);
                bool anyStructuralKid = kids.Any(c => IsStructuralName(NameOf(c)));
                bool anyChildCollection = childRes.Values.Any(v => v.Collection != null);
                bool isCollection = anyStructuralKid
                                    || nonEmptyChildren >= 2
                                    || (anyChildCollection && looseItems.Count > 0);

                ResolvedNode node;
                if (!isCollection)
                {
                    node = new ResolvedNode(null, looseItems);
                }
                else
                {
                    var col = GetOrCreate(folder, InferKind(folder, kids));

                    foreach (var (childPath, cres) in childRes)
                    {
                        if (cres.Collection != null)
                        {
                            cres.Collection.Parent = col;
                        }
                        else if (cres.ItemIds.Count > 0)
                        {
                            if (IsStructuralName(NameOf(childPath)))
                            {
                                var childKind = IsSeasonName(NameOf(childPath)) ? CollectionKind.Season : CollectionKind.Generic;
                                var childCol = GetOrCreate(childPath, childKind);
                                childCol.Parent = col;
                                foreach (int id in cres.ItemIds)
                                    itemById[id].ParentCollection = childCol;
                                AttachArtNfo(childPath, childCol);
                            }
                            else
                            {
                                foreach (int id in cres.ItemIds)
                                    itemById[id].ParentCollection = col;
                            }
                        }
                    }

                    foreach (int id in looseItems)
                        itemById[id].ParentCollection = col;

                    AttachArtNfo(folder, col);
                    node = new ResolvedNode(col, System.Array.Empty<int>());
                }

                memo[folder] = node;
                return node;
            }

            foreach (var folder in allFolders.Where(f => ParentDir(f).Length == 0))
                Resolve(folder);

            await db.SaveChangesAsync();

            // Promote each collection's nfo text onto the collection (its catalogue comment), now that
            // the collections have ids.
            foreach (var (fileId, col) in nfoPromotions)
                await PromoteContentToCollectionAsync(fileId, col.Id, Attr.General.Comment);
            await db.SaveChangesAsync();

            // User collection-level overrides (e.g. a corrected Show title) win and survive reindex (§8).
            foreach (var col in created.Concat(manualByPath.Values))
                await metadataService.ApplyCollectionOverridesAsync(col);

            return created.Count;
        }

        /// <summary>Chooses a collection's kind from its children's naming (collection_plan.md §6.3): a
        /// folder of season/series folders is a Show; otherwise Generic.</summary>
        private static CollectionKind InferKind(string folder, IReadOnlyList<string> childFolders)
        {
            if (childFolders.Any(c => IsSeasonName(NameOf(c))))
                return CollectionKind.Show;
            return CollectionKind.Generic;
        }

        /// <summary>Promotes a sidecar file's extracted text content onto a collection as a text attribute
        /// (mirrors the Item-level <see cref="PromoteContentAsync"/>), stamped with the promotion provider.</summary>
        private async Task<int> PromoteContentToCollectionAsync(int sidecarFileId, int collectionId, int definitionId)
        {
            string? content = await db.IndexedFileContents
                .Where(c => c.FileId == sidecarFileId).Select(c => c.Content).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            bool exists = await db.TextAttributes
                .AnyAsync(a => a.CollectionId == collectionId && a.AttributeDefinitionId == definitionId);
            if (exists)
                return 0;

            db.TextAttributes.Add(new TextAttribute
            {
                CollectionId = collectionId,
                AttributeDefinitionId = definitionId,
                Value = content.Trim(),
                ProviderId = PromotionProvider.ToString(),
                ProviderAttributeId = $"col-nfo:{sidecarFileId}",
                Editable = false,
            });
            await db.SaveChangesAsync();
            return 1;
        }

        #endregion

        private static string ParentDir(string path)
        {
            int slash = path.LastIndexOfAny(new[] { '/', '\\' });
            return slash < 0 ? string.Empty : path[..slash];
        }

        /// <summary>Every ancestor directory of a folder path, nearest first ("a/b/c" → "a/b", "a").</summary>
        private static IEnumerable<string> AncestorDirs(string dir)
        {
            string d = dir;
            while (true)
            {
                int slash = d.LastIndexOfAny(new[] { '/', '\\' });
                if (slash < 0)
                    yield break;
                d = d[..slash];
                yield return d;
            }
        }

        /// <summary>The bundle root that owns a folder: the folder itself if it is a root, else its
        /// nearest ancestor root, else null (not inside a bundle).</summary>
        private static string? NearestRoot(string dir, HashSet<string> roots)
        {
            if (roots.Contains(dir))
                return dir;
            foreach (var a in AncestorDirs(dir))
                if (roots.Contains(a))
                    return a;
            return null;
        }

        private static string LastSegment(string dir)
        {
            int slash = dir.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 && slash < dir.Length - 1 ? dir[(slash + 1)..] : dir;
        }

        private static string NameOf(string path)
        {
            int slash = path.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
        }
    }
}
