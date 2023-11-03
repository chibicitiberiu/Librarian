using Librarian.Models;
using Librarian.Services;
using Librarian.Utils;
using Librarian.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace Librarian.Controllers
{
    public class MetadataController : Controller
    {
        private readonly ILogger<MetadataController> logger;
        readonly FileService fileService;
        readonly MetadataService metadataService;

        public MetadataController(ILogger<MetadataController> logger, MetadataService metadataService, FileService fileService)
        {
            this.logger = logger;
            this.metadataService = metadataService;
            this.fileService = fileService;
        }

        public IActionResult Index(string path)
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
                vm.Metadata = metadataService.GetMetadata(diskPath);

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
    }
}
