using Librarian.DB;
using Librarian.Metadata.Archives;
using Librarian.Model;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeMapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Librarian.Controllers
{
    /// <summary>
    /// The file browser — the application's primary surface. Directories render through the wm
    /// FileListView (details/list/tiles/icons), files open the in-window viewer, and the raw bytes are
    /// still served from <see cref="Index"/> so previews and thumbnails have a stable URL. File
    /// operations (cut/copy/paste/rename/delete) post here too.
    /// </summary>
    public class BrowseController : Controller
    {
        private static readonly string[] ValidModes = { "details", "list", "tiles", "icons" };
        private static readonly string[] ValidSorts = { "name", "size", "type", "modified" };

        // View state persists in cookies (survives browser restarts and keeps the URL clean) rather than
        // query parameters: a click carries one explicit param, which is saved and then redirected away.
        private const string CView = "browse_view";
        private const string CZoom = "browse_zoom";
        private const string CSort = "browse_sort";
        private const string CHidden = "browse_hidden";

        private readonly ILogger<BrowseController> logger;
        readonly FileService fileService;
        readonly MetadataService metadataService;
        readonly DatabaseContext db;
        readonly ArchiveByteReader archiveBytes;

        const string BrowseClipboardKey = "browseClipboard";

        public BrowseController(ILogger<BrowseController> logger, FileService fileService,
                                MetadataService metadataService, DatabaseContext db,
                                ArchiveByteReader archiveBytes)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.metadataService = metadataService;
            this.db = db;
            this.archiveBytes = archiveBytes;
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

        /// <summary>Directory → the wm file listing. File → its raw bytes served inline (the stable URL
        /// used by the viewer's preview and by image thumbnails). Anything else may be a virtual archive
        /// entry whose bytes live inside a parent archive.</summary>
        public IActionResult Index(string path, string? view, int? zoom, string? sort, bool? hidden)
        {
            try
            {
                string diskPath = fileService.Resolve(path);

                if (System.IO.File.Exists(diskPath))
                    return this.InlineFileFromDisk(diskPath);

                if (!Directory.Exists(diskPath))
                    return ServeArchiveEntry(path);

                // A click that changes view state arrives with one explicit param: persist it to a cookie
                // and redirect to the clean URL so the address bar stays free of view-state noise.
                if (view != null || zoom != null || sort != null || hidden != null)
                {
                    if (ValidModes.Contains(view)) SetCookie(CView, view!);
                    if (zoom.HasValue) SetCookie(CZoom, Math.Clamp(zoom.Value, 1, 3).ToString());
                    if (ValidSorts.Contains(sort)) SetCookie(CSort, sort!);
                    if (hidden.HasValue) SetCookie(CHidden, hidden.Value ? "1" : "0");
                    return RedirectToAction(nameof(Index), new { path });
                }

                // Otherwise render from the persisted cookies (falling back to defaults).
                string mode = ValidModes.Contains(GetCookie(CView)) ? GetCookie(CView)! : "details";
                int zoomVal = int.TryParse(GetCookie(CZoom), out var sz) ? Math.Clamp(sz, 1, 3) : 2;
                string sortKey = ValidSorts.Contains(GetCookie(CSort)) ? GetCookie(CSort)! : "name";
                bool showHidden = GetCookie(CHidden) == "1";

                var directoryInfo = new DirectoryInfo(diskPath);
                string relPath = fileService.GetRelativePath(diskPath);
                bool isRoot = fileService.IsRoot(diskPath);

                var vm = new BrowseViewModel
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
                    HasClipboard = GetClipboard() != null,
                    Columns = new List<WmListColumn>
                    {
                        new("Size", numeric: true),
                        new("Type"),
                        new("Modified"),
                    },
                    Items = BuildItems(diskPath, sortKey, showHidden).ToList(),
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

        /// <summary>The in-window file viewer (preview + live metadata). Only real on-disk files have a
        /// viewer; a directory or unknown path falls back to <see cref="Index"/>.</summary>
        public async Task<IActionResult> Viewer(string path)
        {
            try
            {
                string diskPath = fileService.Resolve(path);
                if (System.IO.File.Exists(diskPath))
                    return View(await BuildViewerAsync(diskPath));

                return RedirectToAction(nameof(Index), new { path });
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

        private List<WmListItem> BuildItems(string dirPath, string sort, bool showHidden)
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

            // Home is a plain filesystem view: one row per file. The catalogue's item/collection grouping
            // lives in the Collections view, not here.
            foreach (var file in files)
                result.Add(FileItem(file));

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
                Href = Url.Action("Index", "Browse", new { path = p }),
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

        private WmListItem FileItem(FileInfo file)
        {
            string p = fileService.GetRelativePath(file.FullName);
            string mime = MimeUtility.GetMimeMapping(file.Name);
            string size = HumanizeUtils.HumanizeSize(file.Length);
            string ext = Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant();
            bool isImage = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            string contentLine = string.IsNullOrEmpty(ext) ? size : $"{size} · {ext}";

            return new WmListItem
            {
                Name = file.Name,
                Path = p,
                IsContainer = false,
                // Clicking a file opens the in-window viewer; the context menu's Properties action still
                // goes to the full metadata editor.
                Href = Url.Action("Viewer", "Browse", new { path = p }),
                IconUrl = Url.Content(IconMapping.GetIconUrl(file.Name, mime)),
                LargeIconUrl = BigIcon(Url.Content(IconMapping.GetIconUrl(file.Name, mime))),
                // Real thumbnails are a future job (ImageSharp); for now point image tiles at the file
                // bytes served inline by this controller's Index.
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

        /// <summary>Serves a file as a download (Content-Disposition: attachment) so the browser saves it
        /// rather than rendering it inline. Used by the viewer's "Download" button; "View Raw" still hits
        /// <see cref="Index"/> for inline display.</summary>
        public IActionResult Download(string path)
        {
            try
            {
                string diskPath = fileService.Resolve(path);

                if (System.IO.File.Exists(diskPath))
                    return this.FileFromDisk(diskPath);

                // Virtual archive entry: serve its bytes as an attachment too.
                var entry = db.IndexedFiles
                    .Include(f => f.ParentFile)
                    .FirstOrDefault(f => f.Path == path && f.Source == FileSource.ArchiveEntry);

                if (entry?.ParentFile != null && entry.InternalPath != null)
                {
                    string archiveDisk = fileService.Resolve(entry.ParentFile.Path);
                    if (System.IO.File.Exists(archiveDisk))
                    {
                        Stream? stream = archiveBytes.OpenEntry(archiveDisk, entry.InternalPath);
                        if (stream != null)
                        {
                            string name = Path.GetFileName(entry.InternalPath);
                            Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ContentDisposition] =
                                new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = name }.ToString();
                            return File(stream, MimeUtility.GetMimeMapping(name), name);
                        }
                    }
                }

                return NotFound("File not found!");
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

        /// <summary>Serves the bytes of a virtual archive entry (no standalone disk file) by reading them out
        /// of its parent archive (collection_plan.md §3.1, §7.3). Returns 404 when the path is not a known
        /// archive entry, the archive is missing, or its family has no registered byte source.</summary>
        private IActionResult ServeArchiveEntry(string path)
        {
            var entry = db.IndexedFiles
                .Include(f => f.ParentFile)
                .FirstOrDefault(f => f.Path == path && f.Source == FileSource.ArchiveEntry);

            if (entry?.ParentFile == null || entry.InternalPath == null)
                return NotFound("File not found!");

            string archiveDisk = fileService.Resolve(entry.ParentFile.Path);
            if (!System.IO.File.Exists(archiveDisk))
                return NotFound("Archive not found!");

            Stream? stream = archiveBytes.OpenEntry(archiveDisk, entry.InternalPath);
            if (stream == null)
                return NotFound("Archive entry not readable!");

            return this.InlineStream(stream, Path.GetFileName(entry.InternalPath), entry.Size, entry.Modified);
        }

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

        [HttpPost]
        public IActionResult Cut([FromBody] BrowseCutRequest cutRequest)
        {
            // Validate request
            try
            {
                // Validate source is a valid directory
                cutRequest.Path.ValidateNotNull("No path provided!");

                var diskPath = fileService.Resolve(cutRequest.Path);
                if (!Directory.Exists(diskPath))
                    throw new ArgumentException("Path doesn't exist on disk!");

                // Validate items to cut
                cutRequest.Items.ValidateNotEmpty("No items provided!");
                foreach (var item in cutRequest.Items)
                {
                    item.ValidateFileName("Invalid item " + item);
                }

                // Add to clipboard
                SetClipboard(new BrowseClipboardModel()
                {
                    Move = true,
                    SourceFiles = cutRequest.Items.Select(x => cutRequest.Path + "/" + x).ToArray()
                });

                return Json(new { message = "OK" });
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is PathTooLongException)
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                else if (ex is SecurityException)
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError(ex, "Error handling request!");
                return Json(new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Copy([FromBody] BrowseCopyRequest copyRequest)
        {
            // Validate request
            try
            {
                // Validate source is a valid directory
                copyRequest.Path.ValidateNotNull("No path provided!");

                var diskPath = fileService.Resolve(copyRequest.Path);
                if (!Directory.Exists(diskPath))
                    throw new ArgumentException("Path doesn't exist on disk!");

                // Validate items to cut
                copyRequest.Items.ValidateNotEmpty("No items provided!");
                foreach (var item in copyRequest.Items)
                {
                    item.ValidateFileName("Invalid item " + item);
                }

                // Add to clipboard
                SetClipboard(new BrowseClipboardModel()
                {
                    Move = false,
                    SourceFiles = copyRequest.Items.Select(x => copyRequest.Path + "/" + x).ToArray()
                });

                return Json(new { message = "OK" });
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is PathTooLongException)
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                else if (ex is SecurityException)
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError(ex, "Error handling request!");
                return Json(new { message = ex.Message });
            }
        }

        private BrowseClipboardModel? GetClipboard()
        {
            string? clipboardItemsStr = HttpContext.Session.GetString(BrowseClipboardKey);
            if (clipboardItemsStr == null)
                return null;

            return JsonSerializer.Deserialize<BrowseClipboardModel>(clipboardItemsStr);
        }

        private void SetClipboard(BrowseClipboardModel? content)
        {
            if (content != null)
            {
                string serialized = JsonSerializer.Serialize(content);
                HttpContext.Session.SetString(BrowseClipboardKey, serialized);
            }
            else
            {
                HttpContext.Session.Remove(BrowseClipboardKey);
            }
        }

        [HttpPost]
        public IActionResult Paste([FromBody] BrowsePasteRequest pasteRequest)
        {
            // Validate request
            try
            {
                // Validate source is a valid directory
                pasteRequest.Path.ValidateNotNull("No path provided!");

                var clipboard = GetClipboard();
                clipboard.ValidateNotNull("Nothing to paste!!!");

                // Call backend to perform file operation
                if (clipboard!.Move)
                    fileService.MoveFiles(clipboard.SourceFiles, pasteRequest.Path);
                else fileService.CopyFiles(clipboard.SourceFiles, pasteRequest.Path);

                // Clear clipboard
                SetClipboard(null);

                return Json(new { message = "OK" });
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is PathTooLongException)
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                else if (ex is SecurityException)
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError(ex, "Error handling request!");
                return Json(new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Rename([FromBody] BrowseRenameRequest renameRequest)
        {
            // Validate request
            try
            {
                // Validate source is a valid directory
                renameRequest.Path.ValidateNotNull("No path provided!");
                renameRequest.Item.ValidateNotNull("No item provided!");
                renameRequest.Item.ValidateFileName("Invalid item file name!");
                renameRequest.NewName.ValidateNotNull("New name not provided!");
                renameRequest.NewName.ValidateFileName("New name is not valid!");

                // Call backend to perform file operation
                fileService.RenameFile(renameRequest.Path + "/" + renameRequest.Item, renameRequest.NewName);

                return Json(new { message = "OK", NewName = renameRequest.NewName });
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is PathTooLongException)
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                else if (ex is SecurityException)
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError(ex, "Error handling request!");
                return Json(new { message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Delete([FromBody] BrowseDeleteRequest deleteRequest)
        {
            // Validate request
            try
            {
                // Validate source is a valid directory
                deleteRequest.Path.ValidateNotNull("No path provided!");

                var diskPath = fileService.Resolve(deleteRequest.Path);
                if (!Directory.Exists(diskPath))
                    throw new ArgumentException("Path doesn't exist on disk!");

                // Validate items to cut
                deleteRequest.Items.ValidateNotEmpty("No items provided!");
                foreach (var item in deleteRequest.Items)
                {
                    item.ValidateFileName("Invalid item " + item);
                }

                // Delete
                foreach (var item in deleteRequest.Items)
                {
                    string relPath = deleteRequest.Path + "/" + item;
                    fileService.DeleteFile(relPath);
                }

                return Json(new { message = "OK" });
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is PathTooLongException)
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                else if (ex is SecurityException)
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    Response.StatusCode = StatusCodes.Status500InternalServerError;
                logger.LogError(ex, "Error handling request!");
                return Json(new { message = ex.Message });
            }
        }
    }
}
