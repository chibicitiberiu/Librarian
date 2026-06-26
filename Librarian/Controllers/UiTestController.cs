using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Librarian.Models;
using Librarian.Services;
using Librarian.Utils;
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
        private readonly ILogger<UiTestController> logger;
        private readonly FileService fileService;

        public UiTestController(ILogger<UiTestController> logger, FileService fileService)
        {
            this.logger = logger;
            this.fileService = fileService;
        }

        public IActionResult Index(string path)
        {
            try
            {
                string diskPath = fileService.Resolve(path);

                // Files are handled by the real Browse/Metadata controllers for now; the in-window viewer
                // (Phase E) will render them here instead. Directories drive the browse-clone.
                if (!Directory.Exists(diskPath))
                    return NotFound("Not a directory.");

                var directoryInfo = new DirectoryInfo(diskPath);
                var vm = new BrowseViewModel
                {
                    DisplayName = directoryInfo.Name,
                    DisplayPath = fileService.GetRelativePath(diskPath),
                    ParentPath = (directoryInfo.Parent != null && !fileService.IsRoot(diskPath))
                        ? fileService.GetRelativePath(directoryInfo.Parent.FullName)
                        : null,
                    Path = fileService.GetRelativePath(diskPath),
                    Files = GetFiles(diskPath).ToList(),
                    Clipboard = null
                };

                if (vm.DisplayPath == ".")
                {
                    vm.DisplayName = "Home";
                    vm.DisplayPath = "Home";
                }

                vm.Breadcrumbs = BuildBreadcrumbs(vm.Path).ToList();
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

        private IEnumerable<BrowseFileViewModel> GetFiles(string dirPath)
        {
            var dirInfo = new DirectoryInfo(dirPath);

            foreach (var dir in dirInfo.EnumerateDirectories().OrderBy(x => x.Name, new NaturalComparer()))
            {
                yield return new BrowseFileViewModel
                {
                    Name = dir.Name,
                    Path = fileService.GetRelativePath(dir.FullName),
                    Size = null,
                    DisplaySize = null,
                    LastModified = dir.LastWriteTimeUtc,
                    IsDirectory = true,
                    MimeType = null,
                    IconUrl = Url.Content(IconMapping.FolderIcon)
                };
            }

            foreach (var file in dirInfo.EnumerateFiles().OrderBy(x => x.Name, new NaturalComparer()))
            {
                string fileName = file.Name;
                string mime = MimeUtility.GetMimeMapping(fileName);

                yield return new BrowseFileViewModel
                {
                    Name = fileName,
                    Path = fileService.GetRelativePath(file.FullName),
                    Size = file.Length,
                    DisplaySize = HumanizeUtils.HumanizeSize(file.Length),
                    LastModified = file.LastWriteTimeUtc,
                    IsDirectory = false,
                    MimeType = mime,
                    IconUrl = Url.Content(IconMapping.GetIconUrl(fileName, mime))
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
