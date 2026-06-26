using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public UiTestController(ILogger<UiTestController> logger, FileService fileService)
        {
            this.logger = logger;
            this.fileService = fileService;
        }

        public IActionResult Index(string path, string? view, int? zoom, string? sort)
        {
            string sortKey = ValidSorts.Contains(sort) ? sort! : "name";
            try
            {
                string diskPath = fileService.Resolve(path);

                // Files are handled by the real Browse/Metadata controllers for now; the in-window viewer
                // (Phase E) will render them here instead. Directories drive the browse-clone.
                if (!Directory.Exists(diskPath))
                    return NotFound("Not a directory.");

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
                    Href = $"{Url.Action("Index", "Metadata")}/{p}",
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
