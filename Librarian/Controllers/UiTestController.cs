using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Librarian.DB;
using Librarian.Model;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeMapping;

namespace Librarian.Controllers
{
    /// <summary>
    /// Sandbox for the new window-manager control library (lib/wm) — a functional browse-clone wired to
    /// the real library so the controls can be proven in use before the production pages migrate onto
    /// them. Lives at /uitest and never touches the existing chrome.
    /// </summary>
    public class UiTestController : Controller
    {
        private static readonly string[] ValidModes = { "details", "list", "tiles", "icons" };
        private static readonly string[] ValidSorts = { "name", "size", "type", "modified" };

        // View state persists in cookies (survives browser restarts and keeps the URL clean) rather than
        // query parameters: a click carries one explicit param, which is saved and then redirected away.
        private const string CView = "uitest_view";
        private const string CZoom = "uitest_zoom";
        private const string CSort = "uitest_sort";
        private const string CHidden = "uitest_hidden";
        private const string CItem = "uitest_itemview";

        private readonly ILogger<UiTestController> logger;
        private readonly FileService fileService;
        private readonly MetadataService metadataService;
        private readonly DatabaseContext db;

        public UiTestController(ILogger<UiTestController> logger, FileService fileService,
                                MetadataService metadataService, DatabaseContext db)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.metadataService = metadataService;
            this.db = db;
        }

        private string? GetCookie(string key) =>
            Request.Cookies.TryGetValue(key, out var v) ? v : null;

        private void SetCookie(string key, string value) =>
            Response.Cookies.Append(key, value, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
            });

        public async Task<IActionResult> Index(string path, string? view, int? zoom, string? sort,
                                               bool? hidden, bool? itemview)
        {
            try
            {
                string diskPath = fileService.Resolve(path);

                // A file path opens the in-window viewer; a directory drives the browse-clone.
                if (System.IO.File.Exists(diskPath))
                    return View("Viewer", await BuildViewerAsync(diskPath));

                if (!Directory.Exists(diskPath))
                    return NotFound("Not found.");

                // A click that changes view state arrives with one explicit param: persist it to a cookie
                // and redirect to the clean URL so the address bar stays free of view-state noise.
                if (view != null || zoom != null || sort != null || hidden != null || itemview != null)
                {
                    if (ValidModes.Contains(view)) SetCookie(CView, view!);
                    if (zoom.HasValue) SetCookie(CZoom, Math.Clamp(zoom.Value, 1, 3).ToString());
                    if (ValidSorts.Contains(sort)) SetCookie(CSort, sort!);
                    if (hidden.HasValue) SetCookie(CHidden, hidden.Value ? "1" : "0");
                    if (itemview.HasValue) SetCookie(CItem, itemview.Value ? "1" : "0");
                    return RedirectToAction(nameof(Index), new { path });
                }

                // Otherwise render from the persisted cookies (falling back to defaults).
                string mode = ValidModes.Contains(GetCookie(CView)) ? GetCookie(CView)! : "details";
                int zoomVal = int.TryParse(GetCookie(CZoom), out var sz) ? Math.Clamp(sz, 1, 3) : 2;
                string sortKey = ValidSorts.Contains(GetCookie(CSort)) ? GetCookie(CSort)! : "name";
                bool showHidden = GetCookie(CHidden) == "1";
                bool itemView = GetCookie(CItem) == "1";

                var directoryInfo = new DirectoryInfo(diskPath);
                string relPath = fileService.GetRelativePath(diskPath);
                bool isRoot = fileService.IsRoot(diskPath);

                var vm = new UiTestViewModel
                {
                    DisplayName = isRoot ? "Home" : directoryInfo.Name,
                    Path = relPath,
                    ParentPath = (directoryInfo.Parent != null && !isRoot)
                        ? fileService.GetRelativePath(directoryInfo.Parent.FullName)
                        : null,
                    Breadcrumbs = BuildBreadcrumbs(relPath).ToList(),
                    Mode = mode,
                    Zoom = zoomVal,
                    Sort = sortKey,
                    ShowHidden = showHidden,
                    ItemView = itemView,
                    Columns = new List<WmListColumn>
                    {
                        new("Size", numeric: true),
                        new("Type"),
                        new("Modified"),
                    },
                    Items = BuildItems(diskPath, sortKey, showHidden, itemView).ToList(),
                };

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

        private List<WmListItem> BuildItems(string dirPath, string sort, bool showHidden, bool itemView)
        {
            var result = new List<WmListItem>();
            var dirInfo = new DirectoryInfo(dirPath);
            var nat = new NaturalComparer();

            // Folders always sort before files; within each group the chosen key applies.
            IEnumerable<DirectoryInfo> dirs = dirInfo.EnumerateDirectories();
            IEnumerable<FileInfo> files = dirInfo.EnumerateFiles();

            // Hidden files (dotfiles or the Hidden attribute) are filtered unless "Show hidden" is on.
            if (!showHidden)
            {
                dirs = dirs.Where(d => !IsHidden(d));
                files = files.Where(f => !IsHidden(f));
            }

            switch (sort)
            {
                case "size":
                    dirs = dirs.OrderBy(d => d.Name, nat);
                    files = files.OrderBy(f => f.Length);
                    break;
                case "type":
                    dirs = dirs.OrderBy(d => d.Name, nat);
                    files = files.OrderBy(f => MimeUtility.GetMimeMapping(f.Name), StringComparer.OrdinalIgnoreCase)
                                 .ThenBy(f => f.Name, nat);
                    break;
                case "modified":
                    dirs = dirs.OrderBy(d => d.LastWriteTimeUtc);
                    files = files.OrderBy(f => f.LastWriteTimeUtc);
                    break;
                default:
                    dirs = dirs.OrderBy(d => d.Name, nat);
                    files = files.OrderBy(f => f.Name, nat);
                    break;
            }

            foreach (var dir in dirs)
                result.Add(FolderItem(dir));

            // Item view: fold each catalog Item's files into one entry (represented by its primary file);
            // files that belong to no Item still show individually. With no catalog data it degrades to a
            // plain file list.
            (Dictionary<string, int> memberItemId, Dictionary<int, (string repPath, int count)> itemRep) grouping = default;
            bool grouped = itemView;
            if (grouped)
                grouping = BuildItemGrouping(dirPath);

            var orderedFiles = files.ToList();
            var byRelPath = new Dictionary<string, FileInfo>();
            if (grouped)
                foreach (var f in orderedFiles)
                    byRelPath[fileService.GetRelativePath(f.FullName)] = f;

            var emittedItems = new HashSet<int>();
            foreach (var file in orderedFiles)
            {
                string rel = fileService.GetRelativePath(file.FullName);
                if (grouped && grouping.memberItemId.TryGetValue(rel, out int itemId))
                {
                    if (!emittedItems.Add(itemId))
                        continue;   // already represented by another file of the same Item
                    var (repPath, count) = grouping.itemRep[itemId];
                    var repFi = byRelPath.TryGetValue(repPath, out var rf) ? rf : file;
                    result.Add(FileItem(repFi, count));
                }
                else
                {
                    result.Add(FileItem(file, null));
                }
            }

            return result;
        }

        private static bool IsHidden(FileSystemInfo info) =>
            info.Name.StartsWith(".") || (info.Attributes & FileAttributes.Hidden) != 0;

        private WmListItem FolderItem(DirectoryInfo dir)
        {
            string p = fileService.GetRelativePath(dir.FullName);
            return new WmListItem
            {
                Name = dir.Name,
                Path = p,
                IsContainer = true,
                Href = Url.Action("Index", "UiTest", new { path = p }),
                IconUrl = Url.Content(IconMapping.FolderIcon),
                LargeIconUrl = BigIcon(Url.Content(IconMapping.FolderIcon)),
                ContentLine = "Folder",
                Cells = new List<WmListCell>
                {
                    new("", "-1"),
                    new("Folder", "Folder"),
                    new(dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), dir.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss")),
                },
            };
        }

        /// <summary><paramref name="itemCount"/> non-null marks this entry as an Item-view group (its
        /// primary file standing in for the whole Item); the content line then reports the file count.</summary>
        private WmListItem FileItem(FileInfo file, int? itemCount)
        {
            string p = fileService.GetRelativePath(file.FullName);
            string mime = MimeUtility.GetMimeMapping(file.Name);
            string size = HumanizeUtils.HumanizeSize(file.Length);
            string ext = Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant();
            bool isImage = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            string contentLine = (itemCount is int n && n > 1)
                ? $"Item · {n} files"
                : (string.IsNullOrEmpty(ext) ? size : $"{size} · {ext}");

            return new WmListItem
            {
                Name = file.Name,
                Path = p,
                IsContainer = false,
                // Clicking a file opens the in-window viewer; the context menu's Properties action still
                // goes to the full metadata editor.
                Href = Url.Action("Index", "UiTest", new { path = p }),
                IconUrl = Url.Content(IconMapping.GetIconUrl(file.Name, mime)),
                LargeIconUrl = BigIcon(Url.Content(IconMapping.GetIconUrl(file.Name, mime))),
                // Real thumbnails are a future job (ImageSharp); for now point image tiles at the file
                // bytes served by the Browse controller.
                ThumbnailUrl = isImage ? Url.Action("Index", "Browse", new { path = p }) : null,
                ContentLine = contentLine,
                Cells = new List<WmListCell>
                {
                    new(size, file.Length.ToString("D12")),
                    new(mime, mime),
                    new(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), file.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss")),
                },
            };
        }

        /// <summary>Maps the direct-child files of a directory to their catalog Items: returns (file
        /// relative-path → ItemId) and (ItemId → representative primary file + member count). Used by the
        /// Item view to collapse an Item's sidecars/companions behind its primary file.</summary>
        private (Dictionary<string, int> memberItemId, Dictionary<int, (string repPath, int count)> itemRep)
            BuildItemGrouping(string dirPath)
        {
            var member = new Dictionary<string, int>();
            var rep = new Dictionary<int, (string, int)>();

            string dirRel = fileService.GetRelativePath(dirPath);
            string prefix = (dirRel == "." || string.IsNullOrEmpty(dirRel)) ? "" : dirRel + "/";

            try
            {
                IQueryable<IndexedFile> q = db.IndexedFiles
                    .Where(f => f.Source == FileSource.Filesystem && f.Exists && f.ItemId != null);
                q = prefix == ""
                    ? q.Where(f => !f.Path.Contains("/"))
                    : q.Where(f => f.Path.StartsWith(prefix));

                var rows = q.Select(f => new { f.Path, f.ItemId, f.Role }).ToList();

                // Keep only direct children (no deeper path segment) for subdirectories.
                if (prefix != "")
                    rows = rows.Where(r => !r.Path.Substring(prefix.Length).Contains('/')).ToList();

                foreach (var grp in rows.GroupBy(r => r.ItemId!.Value))
                {
                    foreach (var m in grp)
                        member[m.Path] = grp.Key;

                    var repRow = grp.FirstOrDefault(m => m.Role == FileRole.Primary)
                                 ?? grp.OrderBy(m => m.Path).First();
                    rep[grp.Key] = (repRow.Path, grp.Count());
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Item-view grouping query failed for {}", dirPath);
            }

            return (member, rep);
        }

        private static readonly string[] TextExtensions =
        {
            "txt", "md", "markdown", "json", "xml", "csv", "tsv", "log", "yml", "yaml", "ini", "cfg",
            "srt", "vtt", "sub", "nfo", "cs", "js", "ts", "css", "scss", "html", "htm", "sh", "py", "sql",
        };

        private async Task<WmViewerViewModel> BuildViewerAsync(string diskPath)
        {
            var fi = new FileInfo(diskPath);
            string relPath = fileService.GetRelativePath(diskPath);
            string mime = MimeUtility.GetMimeMapping(fi.Name);
            string kind = PreviewKind(mime, fi.Name);

            // Live file-level metadata (same extraction the metadata page uses), grouped for display.
            var attrs = new List<AttributeBase>();
            try
            {
                await foreach (var a in metadataService.CollectMetadataAsync(diskPath))
                    if (a.SubResource == null)
                        attrs.Add(a);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Metadata collection failed for {}", diskPath);
            }

            var groups = attrs
                .Where(a => a.AttributeDefinition != null)
                .GroupBy(a => a.AttributeDefinition.Group)
                .OrderBy(g => g.Key)
                .Select(g => new WmPropGroup(
                    g.Key,
                    g.OrderBy(x => x.AttributeDefinition.Name)
                     .Select(x => (x.AttributeDefinition.Name, AttrValue(x)))
                     .Where(t => !string.IsNullOrEmpty(t.Item2))
                     // Tika and ExifTool often both report the same field (e.g. Width/Height) — collapse exact dupes.
                     .Distinct()
                     .ToList()))
                .Where(g => g.Items.Count > 0)
                .ToList();

            string? textPreview = null;
            if (kind == "text")
            {
                try
                {
                    using var fs = fi.OpenRead();
                    var buf = new byte[64 * 1024];
                    int read = fs.Read(buf, 0, buf.Length);
                    textPreview = Encoding.UTF8.GetString(buf, 0, read);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Text preview read failed for {}", diskPath);
                    kind = "none";
                }
            }

            return new WmViewerViewModel
            {
                DisplayName = fi.Name,
                Path = relPath,
                ParentPath = fi.Directory != null ? fileService.GetRelativePath(fi.Directory.FullName) : null,
                Breadcrumbs = BuildBreadcrumbs(relPath).ToList(),
                PreviewKind = kind,
                PreviewUrl = Url.Action("Index", "Browse", new { path = relPath })!,
                DownloadUrl = Url.Action("Download", "Browse", new { path = relPath })!,
                TextPreview = textPreview,
                MimeType = mime,
                DisplaySize = HumanizeUtils.HumanizeSize(fi.Length),
                Modified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                IconUrl = Url.Content(IconMapping.GetIconUrl(fi.Name, mime)),
                FullMetadataUrl = $"{Url.Action("Index", "Metadata")}/{relPath}",
                Properties = groups,
            };
        }

        private static string PreviewKind(string mime, string name)
        {
            if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "image";
            if (mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "audio";
            if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "video";
            if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) return "pdf";
            if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return "text";

            string ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
            return TextExtensions.Contains(ext) ? "text" : "none";
        }

        private static string AttrValue(AttributeBase a)
        {
            string v = a switch
            {
                TextAttribute t => t.Value,
                IntegerAttribute n => n.Value.ToString(),
                FloatAttribute f when a.AttributeDefinition.Type == AttributeType.TimeSpan => TimeSpan.FromSeconds(f.Value).ToString(),
                FloatAttribute f => f.Value.ToString(),
                DateAttribute d => d.Value.ToString(),
                BlobAttribute b => $"{b.Value.Length} bytes",
                _ => "",
            };
            return string.IsNullOrEmpty(a.AttributeDefinition.Unit) ? v : $"{v} {a.AttributeDefinition.Unit}";
        }

        // Project icons are sourced from the Bluecurve theme and exist at 16px and 48px under the same
        // freedesktop name; the 48px set keeps list/tile/icon thumbnails crisp when zoomed.
        private static string BigIcon(string url16) =>
            url16.Replace("/icons/16/file-types/", "/icons/48/file-types/");

        private static IEnumerable<(string, string)> BuildBreadcrumbs(string path)
        {
            if (path == ".")
                yield break;

            var sb = new StringBuilder();
            foreach (var element in path.Split('/'))
            {
                sb.Append(element);
                sb.Append('/');
                yield return (element, sb.ToString().TrimEnd('/'));
            }
        }
    }
}
