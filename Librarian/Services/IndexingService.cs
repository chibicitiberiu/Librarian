using GitignoreParserNet;
using Librarian.DB;
using Librarian.Indexing;
using Librarian.Model;
using MetadataExtractor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Librarian.Services
{
    public class IndexingService
    {
        private readonly ILogger logger;
        private readonly IConfiguration config;
        private readonly FileService fileService;
        private readonly DatabaseContext dbContext;
        private readonly ISchedulerFactory schedulerFactory;
        private readonly MetadataService metadataService;

        private bool initialized;
        private readonly SemaphoreSlim initializedLock = new(1);

        private GitignoreParser? gitIgnoreParser;
        private DirectoryInfo? baseDirectory;

        public IndexingService(ILogger<IndexingService> logger,
                               IConfiguration config,
                               FileService fileService,
                               DatabaseContext dbContext,
                               ISchedulerFactory schedulerFactory,
                               MetadataService metadataService)
        {
            this.logger = logger;
            this.config = config;
            this.fileService = fileService;
            this.dbContext = dbContext;
            this.schedulerFactory = schedulerFactory;
            this.metadataService = metadataService;
        }

        private async Task Initialize()
        {
            if (!initialized)
            {
                await initializedLock.WaitAsync();
                try
                {
                    if (!initialized)
                    {
                        baseDirectory = new DirectoryInfo(fileService.BasePath);
                        LoadIgnoreFile().RunSynchronously();
                        initialized = true;
                    }
                }
                finally
                {
                    initializedLock.Release();
                }
            }
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

        public async Task IndexAll()
        {
            await Initialize();
            await IndexDirectory(baseDirectory!, recurse: true);
        }

        public async Task IndexDirectory(DirectoryInfo directory, bool recurse)
        {
            await Initialize();

            if (ShouldIgnoreDirectory(directory))
            {
                logger.LogTrace("Ignoring directory {folderName}", directory.FullName);
                return;
            }

            logger.LogTrace("Indexing directory {folderName}", directory.FullName);

            var indexedFile = dbContext.IndexedFiles.FirstOrDefault(x => x.Path == directory.FullName);
            if (indexedFile == null)
            {
                indexedFile = new IndexedFile()
                {
                    NeedsUpdating = false,
                    Path = directory.FullName,
                };
                dbContext.Add(indexedFile);
            }

            indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
            indexedFile.Created = directory.CreationTimeUtc;
            indexedFile.Modified = directory.LastWriteTimeUtc;
            await dbContext.SaveChangesAsync();

            await metadataService.UpdateMetadata(indexedFile);

            if (recurse)
            {
                var opts = new EnumerationOptions() { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                foreach (var subdir in directory.EnumerateDirectories("*", opts))
                    await IndexDirectory(subdir, true);

                // Process files
                foreach (var file in directory.EnumerateFiles("*", opts))
                    await IndexFile(file);
            }
        }

        private async Task IndexFile(FileInfo file)
        {
            if (ShouldIgnoreFile(file))
            {
                logger.LogTrace("Ignoring file {folderName}", file.FullName);
                return;
            }

            var indexedFile = dbContext.IndexedFiles.FirstOrDefault(x => x.Path == file.FullName);

            if (indexedFile == null || HasFileChanged(file, indexedFile))
            {
                if (indexedFile == null)
                {
                    indexedFile = new IndexedFile() { Path = file.FullName };
                    dbContext.Add(indexedFile);
                }
                indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
                indexedFile.NeedsUpdating = true;
                indexedFile.Created = file.CreationTimeUtc;
                indexedFile.Modified = file.LastWriteTimeUtc;
                indexedFile.Size = file.Length;
                await dbContext.SaveChangesAsync();

                // For files, we schedule a separate job because it might take a while.
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.ScheduleJob(TriggerBuilder.Create()
                    .ForJob(MetadataUpdateJob.Key)
                    .StartNow()
                    .UsingJobData("fileId", indexedFile.Id)
                    .Build());
            }
       }

        private bool ShouldIgnoreDirectory(DirectoryInfo directory)
        {
            if (directory.GetFiles(".noindex").Any())
                return true;

            string relativePath = fileService.GetRelativePath(directory.FullName);
            return !IsAccepted(relativePath);
        }

        private bool ShouldIgnoreFile(FileInfo fileInfo)
        {
            string relativePath = fileService.GetRelativePath(fileInfo.FullName);
            if (!IsAccepted(relativePath))
                return true;

            if (metadataService.IsMetaFile(relativePath))
                return true;

            return false;
        }

        private bool IsAccepted(string relativePath)
        {
            return gitIgnoreParser?.Accepts("/" + relativePath) ?? true;
        }

        private static bool HasFileChanged(FileInfo file, IndexedFile indexedFile)
        {
            return file.LastWriteTime != indexedFile.Modified
                || file.CreationTime != indexedFile.Created
                || file.Length != indexedFile.Size;
        }
    }
}
