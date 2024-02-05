using Librarian.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;

namespace Librarian.Indexing
{
    public class IndexingJob : IJob
    {
        public static readonly JobKey Key = new(nameof(IndexingJob));

        private readonly ILogger<IndexingJob> logger;
        private readonly IndexingService indexingService;

        public IndexingJob(ILogger<IndexingJob> logger,
                           IndexingService indexingService)
        {
            this.logger = logger;
            this.indexingService = indexingService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Indexing started!");
            await indexingService.IndexAll();
            logger.LogInformation("Indexing finished!");
        }
    }
}
