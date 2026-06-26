using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Librarian.Model;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
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

        private readonly ILogger<UiTestController> logger;
        private readonly FileService fileService;
        private readonly MetadataService metadataService;

        public UiTestController(ILogger<UiTestController> logger, FileService fileService,
                                MetadataService metadataService)
        {
            this.logger = logger;
            this.fileService = fileService;
            this.metadataService = metadataService;
        }

        public async Task<IActionResult> Index(string path, string? view, int? zoom, string? sort)
        {
            string sortKey = ValidSorts.Contains(sort) ? sort! : "name";
            try
            {
                string diskPath = fileService.Resolve(path);

                // A file path opens the in-window viewer; a directory drives the browse-clone.
                if (System.IO.File.Exists(diskPath))
                    return View("Viewer", await BuildViewerAsync(diskPath));

                if (!Directory.Exists(diskPath))
                    return NotFound("Not found.");

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
                    Mode = ValidModes.Contains(view) ? view! : "details",
                    Zoom = Math.Clamp(zoom ?? 2, 1, 3),
                    Sort = sortKey,
                    Columns = new List<WmListColumn>
                    {
                        new("Size", numeric: true),
                        new("Type"),
                        new("Modified"),
                    },
                    Items = BuildItems(diskPath, sortKey).ToList(),
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

        private IEnumerable<WmListItem> BuildItems(string dirPath, string sort)
        {
            var dirInfo = new DirectoryInfo(dirPath);
            var nat = new NaturalComparer();

            // Folders always sort before files; within each group the chosen key applies.
            IEnumerable<DirectoryInfo> dirs = dirInfo.EnumerateDirectories();
            IEnumerable<FileInfo> files = dirInfo.EnumerateFiles();
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
            {
                string p = fileService.GetRelativePath(dir.FullName);
                yield return new WmListItem
                {
                    Name = dir.Name,
                    Path = p,
                    IsContainer = true,
                    Href = Url.Action("Index", "UiTest", new { path = p }),
                    IconUrl = Url.Content(IconMapping.FolderIcon),
                    ContentLine = "Folder",
                    Cells = new List<WmListCell>
                    {
                        new("", "-1"),
                        new("Folder", "Folder"),
                        new(dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), dir.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss")),
                    },
                };
            }

            foreach (var file in files)
            {
                string p = fileService.GetRelativePath(file.FullName);
                string mime = MimeUtility.GetMimeMapping(file.Name);
                string size = HumanizeUtils.HumanizeSize(file.Length);
                string ext = Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant();
                bool isImage = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

                yield return new WmListItem
                {
                    Name = file.Name,
                    Path = p,
                    IsContainer = false,
                    // Clicking a file opens the in-window viewer (Phase E); the context menu's Properties
                    // action still goes to the full metadata editor.
                    Href = Url.Action("Index", "UiTest", new { path = p }),
                    IconUrl = Url.Content(IconMapping.GetIconUrl(file.Name, mime)),
                    // Real thumbnails are a future job (ImageSharp); for now point image tiles at the file
                    // bytes served by the Browse controller.
                    ThumbnailUrl = isImage ? Url.Action("Index", "Browse", new { path = p }) : null,
                    ContentLine = string.IsNullOrEmpty(ext) ? size : $"{size} · {ext}",
                    Cells = new List<WmListCell>
                    {
                        new(size, file.Length.ToString("D12")),
                        new(mime, mime),
                        new(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), file.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss")),
                    },
                };
            }
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
