using GitignoreParserNet;
using Librarian.DB;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
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

        // Per-path debounce timers for watcher events (coalesce a write's event burst).
        private readonly ConcurrentDictionary<string, CancellationTokenSource> debounce = new();
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(750);

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

        // Patterns ignored out of the box: partial downloads, editor/OS cruft and lock files that
        // are never library content. The user's IgnoreListFile (if any) is layered on top, so it
        // can add to — but cannot silently lose — these sensible defaults.
        private const string DefaultIgnorePatterns = """
            *.part
            *.crdownload
            *.download
            *.tmp
            *.temp
            *.partial
            ~$*
            .~lock.*
            Thumbs.db
            desktop.ini
            .DS_Store
            @eaDir/
            """;

        private async Task LoadIgnoreFile()
        {
            string patterns = DefaultIgnorePatterns;

            string? ignoreFile = config["Indexing:IgnoreListFile"];
            if (!string.IsNullOrWhiteSpace(ignoreFile) && File.Exists(ignoreFile))
                patterns += "\n" + await File.ReadAllTextAsync(ignoreFile);
            else if (!string.IsNullOrWhiteSpace(ignoreFile))
                logger.LogWarning("Configured ignore list file '{ignoreFile}' was not found; using defaults only.", ignoreFile);

            gitIgnoreParser = new GitignoreParser(patterns);
        }

        private void CreateFileSystemWatcher()
        {
            fsWatcher = new FileSystemWatcher(fileService.BasePath)
            {
                // Deliberately NOT watching LastAccess/Attributes: indexing reads files and
                // directories, which updates their access time and would fire endless change
                // events, causing an infinite re-indexing loop (especially on Linux/inotify).
                NotifyFilter = NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
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

        // Created and Changed are debounced: writing a single file fires a burst of these events, and
        // coalescing them per path avoids re-indexing the same file many times mid-write.
        private void FsWatcher_Created(object sender, FileSystemEventArgs e)
        {
            logger.LogTrace("FsWatcher: created {file} [{change}]", e.FullPath, e.ChangeType);
            ScheduleIndex(e.FullPath);
        }

        private void FsWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            logger.LogTrace("FsWatcher: changed {file} [{change}]", e.FullPath, e.ChangeType);
            ScheduleIndex(e.FullPath);
        }

        /// <summary>Debounce: (re)start a timer per path; only the last event in a burst actually
        /// re-indexes, once the file has been quiet for <see cref="DebounceDelay"/>.</summary>
        private void ScheduleIndex(string fullPath)
        {
            var cts = new CancellationTokenSource();
            debounce.AddOrUpdate(fullPath, cts, (_, existing) => { existing.Cancel(); return cts; });

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelay, cts.Token);
                    debounce.TryRemove(new KeyValuePair<string, CancellationTokenSource>(fullPath, cts));
                    await OnFileChanged(fullPath);
                }
                catch (OperationCanceledException) { /* superseded by a newer event for this path */ }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception occurred while updating index on file change.");
                }
                finally { cts.Dispose(); }
            });
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
                // Drop any pending re-index for a path that no longer exists.
                if (debounce.TryRemove(e.FullPath, out var pending))
                    pending.Cancel();
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

        /// <summary>Full index. <paramref name="force"/> re-extracts every file even when it looks
        /// unchanged — a "full reindex" that rebuilds the canonical layer from scratch.</summary>
        public async Task IndexAll(bool force = false)
        {
            // Give a provider that tripped its circuit breaker on a previous run a clean chance.
            serviceProvider.GetRequiredService<ProviderExecutor>().Reset();

            var jobToken = jobTracker.StartJob(Strings.IndexingAll);

            await IndexDirectoryInternal(new DirectoryInfo(fileService.BasePath), recurse: true, force: force, jobToken: jobToken);

            using (var scope = serviceProvider.CreateScope())
                await PruneMissing(GetScopedServices(scope));

            jobToken.Finish();
        }

        /// <summary>Re-indexes only the files whose last extraction was flagged incomplete (a transient
        /// provider failure, e.g. Tika was down). Resets the circuit breaker first so a recovered
        /// provider gets a clean chance. Returns the number of files re-indexed.</summary>
        public async Task<int> ReindexIncompleteAsync()
        {
            serviceProvider.GetRequiredService<ProviderExecutor>().Reset();

            List<string> paths;
            using (var scope = serviceProvider.CreateScope())
            {
                paths = await GetScopedServices(scope).DbContext.IndexedFiles
                    .Where(f => f.ExtractionIncomplete && f.Exists)
                    .Select(f => f.Path)
                    .ToListAsync();
            }

            if (paths.Count == 0)
                return 0;

            var jobToken = jobTracker.StartJob("Re-indexing incomplete files");
            int count = 0;
            foreach (var relPath in paths)
            {
                string full = fileService.Resolve(relPath);
                if (!File.Exists(full))
                    continue;
                try
                {
                    await IndexFileInternal(new FileInfo(full), force: true);
                    count++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Re-indexing incomplete file {path} failed.", relPath);
                }
            }
            jobToken.Finish();
            logger.LogInformation("Re-indexed {count} previously-incomplete files.", count);
            return count;
        }

        /// <summary>After a full walk, marks files that are no longer on disk as not-existing (soft,
        /// like the delete watcher) — clears out leftovers such as completed/removed <c>.part</c>
        /// downloads and anything deleted while the app was offline.</summary>
        private async Task PruneMissing(ScopedServices scopedServices)
        {
            var indexed = await scopedServices.DbContext.IndexedFiles
                .Where(f => f.Exists)
                .Select(f => new { f.Id, f.Path })
                .ToListAsync();

            var missing = indexed
                .Where(f => { string p = fileService.Resolve(f.Path); return !File.Exists(p) && !Directory.Exists(p); })
                .Select(f => f.Id)
                .ToList();

            if (missing.Count == 0)
                return;

            await scopedServices.DbContext.IndexedFiles
                .Where(f => missing.Contains(f.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(f => f.Exists, false));
            logger.LogInformation("Pruned {count} files no longer on disk.", missing.Count);
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

            await IndexDirectoryInternal(directory, recurse: recurse, force: false, jobToken: jobToken);

            jobToken.Finish();
        }

        public async Task IndexFile(FileInfo file)
        {
            var relativePath = fileService.GetRelativePath(file);
            var jobToken = jobTracker.StartJob(string.Format(Strings.IndexingFile, relativePath));

            await IndexFileInternal(file, force: false);

            jobToken.Finish();
        }

        // Each file/directory is indexed in its OWN scope (and DbContext), so the change tracker never
        // accumulates the whole library — a unit of work per item (RC-6).
        private async Task IndexDirectoryInternal(DirectoryInfo directory, bool recurse, bool force, JobToken jobToken)
        {
            if (ShouldIgnoreDirectory(directory))
            {
                logger.LogTrace("Ignoring directory {folderName}", directory.FullName);
                return;
            }

            logger.LogTrace("Indexing directory {folderName}", directory.FullName);
            using (var scope = serviceProvider.CreateScope())
                await UpdateIndex(directory, force, GetScopedServices(scope));

            if (recurse)
            {
                Queue<FileSystemInfo> queue = new();
                queue.Enqueue(directory);

                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    jobToken.AdvanceProgress(1, fileService.GetRelativePath(item.FullName));

                    try
                    {
                        if (item is FileInfo file)
                        {
                            await IndexFileInternal(file, force);
                        }
                        else if (item is DirectoryInfo dir)
                        {
                            await IndexDirectoryInternal(dir, false, force, jobToken);

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

        private async Task IndexFileInternal(FileInfo file, bool force)
        {
            using var scope = serviceProvider.CreateScope();
            var scopedServices = GetScopedServices(scope);

            if (ShouldIgnoreFile(file, scopedServices))
            {
                logger.LogTrace("Ignoring file {fileName}", file.FullName);
                return;
            }

            logger.LogTrace("Indexing file {fileName}", file.FullName);
            await UpdateIndex(file, force, scopedServices);
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

        private async Task UpdateIndex(DirectoryInfo directory, bool force, ScopedServices scopedServices)
        {
            var relPath = fileService.GetRelativePath(directory);

            var indexedFile = scopedServices.DbContext.IndexedFiles.FirstOrDefault(x => x.Path == relPath);
            // Re-extract only when the directory actually changed (its mtime moves when its contents
            // change) — otherwise every full index needlessly re-ran metadata for every folder.
            bool changed = force || indexedFile == null || HasTimestampChanged(directory.LastWriteTimeUtc, indexedFile.Modified);

            if (indexedFile == null)
            {
                indexedFile = new IndexedFile() { Path = relPath };
                scopedServices.DbContext.Add(indexedFile);
            }

            indexedFile.Exists = true;
            indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
            indexedFile.Created = directory.CreationTimeUtc;
            indexedFile.Modified = directory.LastWriteTimeUtc;
            await scopedServices.DbContext.SaveChangesAsync();

            if (changed)
                await scopedServices.MetadataService.UpdateMetadata(indexedFile);
        }

        private async Task UpdateIndex(FileInfo file, bool force, ScopedServices scopedServices)
        {
            var relPath = fileService.GetRelativePath(file);
            var indexedFile = scopedServices.DbContext.IndexedFiles.FirstOrDefault(x => x.Path == relPath);

            if (force || indexedFile == null || HasFileChanged(file, indexedFile))
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
                // Content changed → its hashes are stale. Clear the prefix hash (the full-hash Checksum
                // attribute is dropped by UpdateMetadata's canonical rebuild); the checksum pass recomputes.
                indexedFile.PrefixHash = null;
                await scopedServices.DbContext.SaveChangesAsync();

                await scopedServices.MetadataService.UpdateMetadata(indexedFile);
            }
        }

        private static bool HasFileChanged(FileInfo file, IndexedFile indexedFile)
        {
            // Compare instants with a 1s tolerance: the DB can store coarser precision than the
            // filesystem, which otherwise made every file look "changed" and re-extract each index.
            return HasTimestampChanged(file.LastWriteTimeUtc, indexedFile.Modified)
                || file.Length != indexedFile.Size;
        }

        private static bool HasTimestampChanged(DateTime diskUtc, DateTimeOffset indexed)
            => Math.Abs((diskUtc - indexed.UtcDateTime).Ticks) > TimeSpan.TicksPerSecond;

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
