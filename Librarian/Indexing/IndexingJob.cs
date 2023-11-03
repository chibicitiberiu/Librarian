using GitignoreParserNet;
using Librarian.DB;
using Librarian.Model;
using Librarian.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Indexing
{
    public class IndexingJob : IJob
    {
        public static readonly JobKey Key = new(nameof(IndexingJob));

        private readonly ILogger<IndexingJob> logger;
        private readonly IConfiguration config;
        private readonly FileService fileService;
        private readonly DatabaseContext dbContext;
        private readonly ISchedulerFactory schedulerFactory;
        private GitignoreParser? gitIgnoreParser;

        private int processed = 0;

        public IndexingJob(ILogger<IndexingJob> logger,
                           IConfiguration config,
                           FileService fileService,
                           DatabaseContext dbContext,
                           ISchedulerFactory schedulerFactory)
        {
            this.logger = logger;
            this.config = config;
            this.fileService = fileService;
            this.dbContext = dbContext;
            this.schedulerFactory = schedulerFactory;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Indexing started!");
            await ParseIgnoreFile();
            await ProcessDirectory(new DirectoryInfo(fileService.BasePath));
            logger.LogInformation("Indexing finished!");
        }

        private async Task ParseIgnoreFile()
        {
            string? ignoreFile = config["Indexing:IgnoreListFile"];
            if (string.IsNullOrWhiteSpace(ignoreFile))
                return;

            string content = await File.ReadAllTextAsync(ignoreFile);
            gitIgnoreParser = new GitignoreParser(content);
        }

        private async Task ProcessDirectory(DirectoryInfo dir)
        {
            ++processed;
            logger.LogInformation("Indexing dir {0}:{1}", processed, dir.FullName);

            // Index folder
            var indexedFile = dbContext.IndexedFiles.FirstOrDefault(x => x.Path == dir.FullName);
            if (indexedFile == null)
            {
                indexedFile = new IndexedFile()
                {
                    IndexLastUpdated = DateTimeOffset.UtcNow,
                    NeedsUpdating = false,
                    Path = dir.FullName,
                    Created = dir.CreationTimeUtc,
                    Modified = dir.LastWriteTimeUtc
                };
                dbContext.Add(indexedFile);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
                indexedFile.Created = dir.CreationTimeUtc;
                indexedFile.Modified = dir.LastWriteTimeUtc;
                await dbContext.SaveChangesAsync();
            }

            // Process subdirectories
            var opts = new EnumerationOptions() { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
            foreach (var subdir in dir.EnumerateDirectories("*", opts))
            {
                var relPath = fileService.GetRelativePath(subdir.FullName);
                if (IsAccepted(relPath))
                    await ProcessDirectory(subdir);
            }

            // Process files
            foreach (var file in dir.EnumerateFiles("*", opts))
            {
                var relPath = fileService.GetRelativePath(file.FullName);
                if (IsAccepted(relPath))
                    await ProcessFile(file);
            }
        }

        private async Task ProcessFile(FileInfo file)
        {
            ++processed;
            logger.LogInformation("Indexing file {0}:{1}", processed, file.FullName);

            var indexedFile = dbContext.IndexedFiles.FirstOrDefault(x => x.Path == file.FullName);
            bool needsUpdating = false;

            if (indexedFile == null)
            {
                indexedFile = new IndexedFile()
                {
                    IndexLastUpdated = DateTimeOffset.UtcNow,
                    NeedsUpdating = true,
                    Path = file.FullName,
                    Created = file.CreationTimeUtc,
                    Modified = file.LastWriteTimeUtc,
                    Size = file.Length,
                };
                dbContext.Add(indexedFile);
                await dbContext.SaveChangesAsync();

                needsUpdating = true;
            }
            else if (HasFileChanged(file, indexedFile))
            {
                indexedFile.IndexLastUpdated = DateTimeOffset.UtcNow;
                indexedFile.NeedsUpdating = true;
                indexedFile.Created = file.CreationTimeUtc;
                indexedFile.Modified = file.LastWriteTimeUtc;
                indexedFile.Size = file.Length;
                await dbContext.SaveChangesAsync();

                needsUpdating = true;
            }

            if (needsUpdating)
            {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.ScheduleJob(TriggerBuilder.Create()
                    .ForJob(MetadataUpdateJob.Key)
                    .StartNow()
                    .UsingJobData("fileId", indexedFile.Id)
                    .Build());
            }
        }

        private bool HasFileChanged(FileInfo file, IndexedFile indexedFile)
        {
            return file.LastWriteTime != indexedFile.Modified
                || file.CreationTime != indexedFile.Created
                || file.Length != indexedFile.Size;
        }

        private bool IsAccepted(string relPath)
        {
            return gitIgnoreParser?.Accepts("/" + relPath) ?? true;
        }
    }
}
