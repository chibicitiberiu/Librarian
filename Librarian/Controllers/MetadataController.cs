using Librarian.DB;
using Librarian.Library;
using Librarian.Model;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeMapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Controllers
{
    public class MetadataController : Controller
    {
        private readonly ILogger<MetadataController> logger;
        readonly FileService fileService;
        readonly MetadataService metadataService;
        readonly CollectionService collectionService;
        readonly DatabaseContext db;

        public MetadataController(ILogger<MetadataController> logger, MetadataService metadataService,
                                  CollectionService collectionService, FileService fileService, DatabaseContext db)
        {
            this.logger = logger;
            this.metadataService = metadataService;
            this.collectionService = collectionService;
            this.fileService = fileService;
            this.db = db;
        }

        public async Task<IActionResult> IndexAsync(string path)
        {
            try
            {
                var vm = new MetadataViewModel();

                string diskPath = fileService.Resolve(path);

                FileSystemInfo fsInfo;
                if (System.IO.File.Exists(diskPath))
                {
                    fsInfo = new FileInfo(diskPath);
                    vm.IsDirectory = false;

                    var parent = ((FileInfo)fsInfo).Directory;
                    vm.ParentPath = parent != null ? fileService.GetRelativePath(parent.FullName) : null;
                }
                else if (Directory.Exists(diskPath))
                {
                    fsInfo = new DirectoryInfo(diskPath);
                    vm.IsDirectory = true;
                }
                else
                {
                    logger.LogError("File not found: {}", diskPath);
                    return NotFound("File not found!");
                }

                vm.DisplayName = fsInfo.Name;
                vm.Path = fileService.GetRelativePath(diskPath);

                vm.DisplayPath = fileService.GetRelativePath(diskPath);
                if (vm.DisplayPath == ".")
                {
                    vm.DisplayName = "Home";
                    vm.DisplayPath = "Home";
                }

                // collect and fill metadata
                var allMetadata = await metadataService.CollectMetadataAsync(diskPath).ToListAsync();
                vm.Metadata = allMetadata.Where(x => x.SubResource == null);
                vm.SubResourceMetadata = allMetadata
                    .Where(x => x.SubResource != null)
                    .GroupBy(x => x.SubResource!)
                    .ToDictionary(grouping => grouping.Key, grouping => grouping.ToArray().AsEnumerable());

                PopulateItem(vm);
                await PopulateCollectionContextAsync(vm);

                return View(vm);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Bad request!");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling request!");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Tier-0 save: persists the edited metadata fields to the folder's hidden <c>.librarian.meta</c>
        /// sidecar (user authorship survives re-indexing). The form posts one entry per field, named
        /// "<c>Group/Name</c>"; multi-valued fields post the same name several times.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(string path)
        {
            try
            {
                string diskPath = fileService.Resolve(path);
                string relative = fileService.GetRelativePath(diskPath);

                var file = db.IndexedFiles.FirstOrDefault(f => f.Path == relative);
                if (file == null)
                    return NotFound("File is not indexed; nothing to edit.");

                var edits = new List<MetadataService.UserMetadataEdit>();
                foreach (var key in Request.Form.Keys)
                {
                    int slash = key.IndexOf('/');
                    if (slash <= 0 || slash >= key.Length - 1)
                        continue;   // not a "Group/Name" field (e.g. the hidden path / antiforgery token)

                    string group = key[..slash];
                    string name = key[(slash + 1)..];
                    var values = Request.Form[key].Where(v => v != null).Select(v => v!).ToList();
                    edits.Add(new MetadataService.UserMetadataEdit(group, name, values));
                }

                await metadataService.SaveUserEditsAsync(file, edits);

                return RedirectToAction("Index", new { path });
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Bad request!");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving metadata!");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>Fills the Item Viewer pane: the Item's files grouped by role, and the cover to
        /// preview (the file itself when it's an image, otherwise the Item's best cover-art companion).</summary>
        private void PopulateItem(MetadataViewModel vm)
        {
            if (!vm.IsDirectory && Sidecars.IsImage(vm.DisplayName))
                vm.CoverPath = vm.Path;

            var indexed = db.IndexedFiles.FirstOrDefault(f => f.Path == vm.Path);
            if (indexed?.ItemId is not int itemId)
                return;

            var members = db.IndexedFiles
                .Where(f => f.ItemId == itemId && f.Exists)
                .Select(f => new { f.Path, f.Role })
                .ToList();

            ItemFileRow Row(string path, FileRole role)
            {
                string name = NameOf(path);
                return new ItemFileRow
                {
                    Name = name,
                    Path = path,
                    IsPrimary = role == FileRole.Primary,
                    IsCurrent = path == vm.Path,
                    IconUrl = Url.Content(IconMapping.GetIconUrl(name, MimeUtility.GetMimeMapping(name))),
                };
            }

            vm.HasItem = true;
            vm.Content = members.Where(m => m.Role == FileRole.Primary).Select(m => Row(m.Path, m.Role)).ToList();
            vm.Sidecars = members.Where(m => m.Role == FileRole.Sidecar).Select(m => Row(m.Path, m.Role))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            vm.Resources = members.Where(m => m.Role == FileRole.Companion).Select(m => Row(m.Path, m.Role))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();

            vm.CoverPath ??= BestCover(members.Where(m => m.Role == FileRole.Companion).Select(m => m.Path));

            // Fallback: shared album/artist art isn't owned by any single track's Item, so look for
            // cover art directly in this file's folder.
            if (vm.CoverPath == null)
            {
                string folder = ParentDir(vm.Path);
                var folderArt = db.IndexedFiles
                    .Where(f => f.Exists && f.Role == FileRole.Companion && f.Path.StartsWith(folder + "/"))
                    .Select(f => f.Path)
                    .ToList()
                    .Where(p => ParentDir(p) == folder);
                vm.CoverPath = BestCover(folderArt);
            }
        }

        /// <summary>Adds the "Part of:" collection breadcrumb and, when the item still has no cover, falls
        /// back to the nearest ancestor collection's cover (collection_plan.md §9.2).</summary>
        private async Task PopulateCollectionContextAsync(MetadataViewModel vm)
        {
            var indexed = db.IndexedFiles.FirstOrDefault(f => f.Path == vm.Path);
            if (indexed?.ItemId is not int itemId)
                return;

            var (crumbs, cover) = await collectionService.GetItemContextAsync(itemId);
            vm.CollectionCrumbs = crumbs;
            vm.CoverPath ??= cover;
        }

        private static string ParentDir(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? string.Empty : path[..slash];
        }

        /// <summary>Picks the best cover-art image among an Item's companions (cover &gt; folder &gt; … &gt; first).</summary>
        private static string? BestCover(IEnumerable<string> companionPaths)
        {
            string[] preference = { "cover", "folder", "front", "poster", "albumart", "thumb", "logo" };

            return companionPaths
                .Where(p => Sidecars.IsImage(NameOf(p)))
                .OrderBy(p =>
                {
                    int i = Array.IndexOf(preference, Path.GetFileNameWithoutExtension(p).ToLowerInvariant());
                    return i < 0 ? int.MaxValue : i;
                })
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static string NameOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
        }
    }
}
