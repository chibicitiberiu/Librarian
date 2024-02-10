using Librarian.DB;
using Librarian.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;

namespace Librarian.Indexing
{
    public class MetadataUpdateJob : IJob
    {
        public static readonly JobKey Key = new(nameof(MetadataUpdateJob));
        private readonly ILogger<MetadataUpdateJob> logger;
        private readonly DatabaseContext dbContext;
        private readonly MetadataService metadataService;
        private readonly IConfiguration config;

        public MetadataUpdateJob(ILogger<MetadataUpdateJob> logger,
                                 DatabaseContext dbContext,
                                 MetadataService metadataService,
                                 IConfiguration config)
        {
            this.logger = logger;
            this.dbContext = dbContext;
            this.metadataService = metadataService;
            this.config = config;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var fileId = context.MergedJobDataMap.GetInt("fileId");
            var indexedFile = await dbContext.IndexedFiles.FindAsync(fileId);
            if (indexedFile == null)
            {
                logger.LogError("Invalid file ID {fileId}", fileId);
                return;
            }

            logger.LogInformation("Getting metadata for {file}...", indexedFile.Path);
            await metadataService.UpdateMetadata(indexedFile);
        }
    }
}
