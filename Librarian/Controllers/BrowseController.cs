using Librarian.Models;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;
using MimeMapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;

namespace Librarian.Controllers
{
    public class BrowseController : Controller
    {
        private readonly ILogger<BrowseController> logger;
        readonly FileService fileService;

        const string BrowseClipboardKey = "browseClipboard";

        public BrowseController(ILogger<BrowseController> logger, FileService fileService)
        {
            this.logger = logger;
            this.fileService = fileService;
        }

        private IEnumerable<BrowseFileViewModel> GetFiles(string dirPath)
        {
            DirectoryInfo dirInfo = new(dirPath);

            var directories = dirInfo.EnumerateDirectories()
                .OrderBy(x => x.Name, new NaturalComparer());

            foreach (var dir in directories)
            {
                yield return new BrowseFileViewModel()
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

            var files = dirInfo.EnumerateFiles()
                .OrderBy(x => x.Name, new NaturalComparer());

            foreach (var file in files)
            {
                string fileName = file.Name;
                string mime = MimeUtility.GetMimeMapping(fileName);

                yield return new BrowseFileViewModel()
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

        public IActionResult Index(string path)
        {
            try
            {
                string diskPath = fileService.Resolve(path);

                if (System.IO.File.Exists(diskPath))
                {
                    return this.InlineFileFromDisk(diskPath);
                }
                else
                {
                    DirectoryInfo directoryInfo = new(diskPath);

                    var vm = new BrowseViewModel
                    {
                        DisplayName = directoryInfo.Name,
                        DisplayPath = fileService.GetRelativePath(diskPath),
                        ParentPath = (directoryInfo.Parent != null && !fileService.IsRoot(diskPath))
                            ? fileService.GetRelativePath(directoryInfo.Parent.FullName)
                            : null,
                        Path = fileService.GetRelativePath(diskPath),
                        Files = GetFiles(diskPath),
                        Clipboard = GetClipboard()
                    };

                    if (vm.DisplayPath == ".")
                    {
                        vm.DisplayName = "Home";
                        vm.DisplayPath = "Home";
                    }

                    vm.Breadcrumbs = BuildBreadcrumbs(vm.Path);
                    return View(vm);
                }
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

        private static IEnumerable<(string, string)> BuildBreadcrumbs(string path)
        {
            StringBuilder sb = new();
            if (path == ".")
            {
                yield break;
            }

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
