using GitignoreParserNet;
using Librarian.DB;
using Librarian.Jobs;
using Librarian.Model;
using Librarian.Resources;
using Librarian.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Librarian.Services
{
    public sealed class IndexingService : IHostedService, IDisposable
    {
        private readonly ILogger logger;
        private readonly IConfiguration config;
        private readonly FileService fileService;
        private readonly JobTracker jobTracker;
        private readonly IServiceProvider serviceProvider;

        class ScopedServices
        {
            public DatabaseContext DbContext { get; set; } = null!;
            public MetadataService MetadataService { get; set; } = null!;
        }

        private GitignoreParser? gitIgnoreParser;
        private FileSystemWatcher? fsWatcher;

        private readonly bool removeIndexOnDelete;

        public IndexingService(ILogger<IndexingService> logger,
                               IConfiguration config,
                               FileService fileService,
                               JobTracker jobTracker,
                               IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.config = config;
            this.fileService = fileService;
            this.jobTracker = jobTracker;

            if (!bool.TryParse(config["Indexing:RemoveIndexOnDelete"], out removeIndexOnDelete))
                removeIndexOnDelete = false;

            this.serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Initialize();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region Initialization & disposal

        private async Task Initialize()
        {
            await LoadIgnoreFile();
            CreateFileSystemWatcher();
        }

        private async Task LoadIgnoreFile()
        {
            // not configured
            string? ignoreFile = config["Indexing:IgnoreListFile"];
            if (string.IsNullOrWhiteSpace(ignoreFile))
                return;

            string content = await File.ReadAllTextAsync(ignoreFile);
            gitIgnoreParser = new GitignoreParser(content);
        }

        private void CreateFileSystemWatcher()
        {
            fsWatcher = new FileSystemWatcher(fileService.BasePath)
            {
                NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            fsWatcher.Changed += FsWatcher_Changed;
            fsWatcher.Created += FsWatcher_Created;
            fsWatcher.Deleted += FsWatcher_Deleted;
            fsWatcher.Renamed += FsWatcher_Renamed;
            fsWatcher.Error += FsWatcher_Error;
        }

        public void Dispose()
        {
            fsWatcher?.Dispose();
        }

        #endregion

        #region Scoped services

        private static ScopedServices GetScopedServices(IServiceScope scope)
        {
            return new ScopedServices()
            {
                DbContext = scope.ServiceProvider.GetService<DatabaseContext>()!,
                MetadataService = scope.ServiceProvider.GetService<MetadataService>()!
            };
        }

        #endregion

        #region File system watching

        private async void FsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                logger.LogTrace("FsWatcher: created {file} [{change}]", e.FullPath, e.ChangeType);
                await OnFileCreated(e.FullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while updating index on file created.");
            }
        }

        private async void FsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                logger.LogTrace("FsWatcher: changed {file} [{change}]", e.FullPath, e.ChangeType);
                await OnFileChanged(e.FullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while updating index on file changed.");
            }
        }

        private async void FsWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            try
            {
                logger.LogTrace("FsWatcher: renamed {file} [{change}]", e.FullPath, e.ChangeType);
                await OnFileRenamed(e.FullPath, e.OldFullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while updating index on file renamed.");
            }
        }

        private async void FsWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                logger.LogTrace("FsWatcher: deleted {file} [{change}]", e.FullPath, e.ChangeType);
                await OnFileDeleted(e.FullPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred while updating index on file deleted.");
            }
        }

        private void FsWatcher_Error(object sender, ErrorEventArgs e)
        {
            logger.LogError(e.GetException(), "File system watcher error");
        }

        #endregion

        #region Indexing

        public async Task IndexAll()
        {
            var jobToken = jobTracker.StartJob(Strings.IndexingAll);

            using var scope = serviceProvider.CreateScope();
            var scopedServices = GetScopedServices(scope);

            await IndexDirectoryInternal(new DirectoryInfo(fileService.BasePath), recurse: true, jobToken: jobToken, scopedServices: scopedServices);

            jobToken.Finish();
        }

        public async Task Index(FileSystemInfo fsInfo, bool recurseIfDirectory)
        {
            if (fsInfo is DirectoryInfo directory)
                await IndexDirectory(directory, recurseIfDirectory);
            else if (fsInfo is FileInfo fileInfo)
                await IndexFile(fileInfo);
        }

        public async Task IndexDirectory(DirectoryInfo directory, bool recurse)
        {
            var relativePath = fileService.GetRelativePath(directory);
            var jobToken = jobTracker.StartJob(string.Format(Strings.IndexingDirectory, relativePath));

            using var scope = serviceProvider.CreateScope();
            var scopedServices = GetScopedServices(scope);

            await IndexDirectoryInternal(directory, recurse: recurse, jobToken: jobToken, scopedServices: scopedServices);

            jobToken.Finish();
        }

        public async Task IndexFile(FileInfo file)
        {
            var relativePath = fileService.GetRelativePath(file);
            var jobToken = jobTracker.StartJob(string.Format(Strings.IndexingFile, relativePath));

            using var scope = serviceProvider.CreateScope();
            var scopedServices = GetScopedServices(scope);

            await IndexFileInternal(file, scopedServices: scopedServices);

            jobToken.Finish();
        }

        private async Task IndexDirectoryInternal(DirectoryInfo directory, bool recurse, JobToken jobToken, ScopedServices scopedServices)
        {
            if (ShouldIgnoreDirectory(directory))
            {
                logger.LogTrace("Ignoring directory {folderName}", directory.FullName);
                return;
            }

            logger.LogTrace("Indexing directory {folderName}", directory.FullName);
            await UpdateIndex(directory, scopedServices);

            if (recurse)
            {
                Queue<FileSystemInfo> queue = new();
                queue.Enqueue(directory);

                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();

                    var relativePath = fileService.GetRelativePath(directory);
                    jobToken.AdvanceProgress(1, relativePath);

                    try
                    {
                        if (item is FileInfo file)
                        {
                            await IndexFileInternal(file, scopedServices);
                        }
                        else if (item is DirectoryInfo dir)
                        {
                            await IndexDirectoryInternal(dir, false, jobToken, scopedServices);

                            var opts = new EnumerationOptions() { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                            queue.EnqueueRange(dir.EnumerateDirectories("*", opts));
                            queue.EnqueueRange(dir.EnumerateFiles("*", opts));

                            jobToken.TotalUnits = jobToken.Done + queue.Count;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Indexing item {path} failed.", item.FullName);
                    }
                }
            }
        }

        private async Task IndexFileInternal(FileInfo file, ScopedServices scopedServices)
        {
            if (ShouldIgnoreFile(file, scopedServices))
            {
                logger.LogTrace("Ignoring file {fileName}", file.FullName);
                return;
            }

            logger.LogTrace("Indexing file {fileName}", file.FullName);

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DatabaseContext>()!;
            await UpdateIndex(file, scopedServices);
        }

        #endregion

        #region Filtering

        private bool ShouldIgnoreDirectory(DirectoryInfo directory)
        {
            if (directory.GetFiles(".noindex").Any())
                return true;

            string relativePath = fileService.GetRelativePath(directory.FullName);
            return !IsAccepted(relativePath);
        }

        private bool ShouldIgnoreFile(FileInfo fileInfo, ScopedServices scopedServices)
        {
            string relativePath = fileService.GetRelativePath(fileInfo.FullName);
            if (!IsAccepted(relativePath))
                return true;

            if (scopedServices.MetadataService.IsMetaFile(relativePath))
                return true;

            return false;
        }

        private bool IsAccepted(string relativePath)
        {
            return gitIgnoreParser?.Accepts("/" + relativePath) ?? true;
        }

        #endregion

        #region Updating index

        private async Task UpdateIndex(DirectoryInfo directory, ScopedServices scopedServices)
        {
            var relPath = fileService.GetRelativePath(directory);

            var indexedFile = scopedServices.DbContext.IndexedFiles.FirstOrDefault(x => x.Path == relPath);
            if (indexedFile == null)
            {
                indexedFile = new IndexedFile() { Path = relPath, };
                scopedServices.DbContext.Add(indexedFile);
            }

            indexedFile.Exists = true;
            indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
            indexedFile.Created = directory.CreationTimeUtc;
            indexedFile.Modified = directory.LastWriteTimeUtc;
            await scopedServices.DbContext.SaveChangesAsync();

            await scopedServices.MetadataService.UpdateMetadata(indexedFile);
        }

        private async Task UpdateIndex(FileInfo file, ScopedServices scopedServices)
        {
            var relPath = fileService.GetRelativePath(file);
            var indexedFile = scopedServices.DbContext.IndexedFiles.FirstOrDefault(x => x.Path == relPath);

            if (indexedFile == null || HasFileChanged(file, indexedFile))
            {
                if (indexedFile == null)
                {
                    indexedFile = new IndexedFile() { Path = relPath };
                    scopedServices.DbContext.Add(indexedFile);
                }
                indexedFile.Exists = true;
                indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
                indexedFile.NeedsUpdating = true;
                indexedFile.Created = file.CreationTimeUtc;
                indexedFile.Modified = file.LastWriteTimeUtc;
                indexedFile.Size = file.Length;
                await scopedServices.DbContext.SaveChangesAsync();

                await scopedServices.MetadataService.UpdateMetadata(indexedFile);
            }
        }

        private static bool HasFileChanged(FileInfo file, IndexedFile indexedFile)
        {
            return file.LastWriteTime != indexedFile.Modified
                || file.CreationTime != indexedFile.Created
                || file.Length != indexedFile.Size;
        }

        #endregion

        #region Rename, delete

        public async Task OnFileRenamed(string fullPath, string absoluteOldPath)
        {
            await OnFileRenamed(CreateFileSystemInfo(fullPath), absoluteOldPath);
        }

        public async Task OnFileRenamed(FileSystemInfo info, string absoluteOldPath)
        {
            // file moved to dir outside BaseDirectory
            if (!fileService.IsInBaseDirectory(info.FullName))
            {
                await OnFileDeleted(info.FullName);
                return;
            }

            // file moved from outside
            if (!fileService.IsInBaseDirectory(absoluteOldPath))
            {
                await OnFileCreated(info);
                return;
            }

            // update index for all files inside tree
            var relPath = fileService.GetRelativePath(absoluteOldPath);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetService<DatabaseContext>()!;

                Queue<IndexedFile> queue = new();
                dbContext.IndexedFiles
                    .Where(x => x.Path.StartsWith(relPath))
                    .ForEach(queue.Enqueue);

                // nothing found in db? means it's not indexed
                while (queue.Count > 0)
                {
                    var file = queue.Dequeue();
                    var oldFilePath = fileService.Resolve(file.Path);
                    var relToOldPath = oldFilePath[absoluteOldPath.Length..];
                    var newAbsPath = string.IsNullOrEmpty(relToOldPath) ? info.FullName : Path.Combine(info.FullName, relToOldPath);
                    file.Path = fileService.GetRelativePath(info.FullName);
                }

                await dbContext.SaveChangesAsync();
            }

            // refresh index
            await Index(info, true);
        }

        public async Task OnFileCreated(string fullPath)
        {
            await OnFileCreated(CreateFileSystemInfo(fullPath));
        }

        public async Task OnFileCreated(FileSystemInfo item)
        {
            await Index(item, true);
        }

        public async Task OnFileChanged(string fullPath)
        {
            await OnFileChanged(CreateFileSystemInfo(fullPath));
        }

        public async Task OnFileChanged(FileSystemInfo item)
        {
            await Index(item, true);
        }

        public async Task OnFileDeleted(string absolutePath)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<DatabaseContext>()!;

            var relPath = fileService.GetRelativePath(absolutePath);

            if (removeIndexOnDelete)
            {
                dbContext.RemoveRange(dbContext.IndexedFiles
                    .Where(x => x.Path.StartsWith(relPath)));
            }
            else
            {
                dbContext.IndexedFiles
                    .Where(x => x.Path.StartsWith(relPath))
                    .ForEach(x => x.Exists = false);
            }
            await dbContext.SaveChangesAsync();
        }

        #endregion

        #region Other operations

        private FileSystemInfo CreateFileSystemInfo(string path)
        {
            if (Directory.Exists(path))
                return new DirectoryInfo(path);

            return new FileInfo(path);
        }

        #endregion
    }
}
