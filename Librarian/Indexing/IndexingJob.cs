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
        private readonly ItemAssociationService itemAssociation;

        public IndexingJob(ILogger<IndexingJob> logger,
                           IndexingService indexingService,
                           ItemAssociationService itemAssociation)
        {
            this.logger = logger;
            this.indexingService = indexingService;
            this.itemAssociation = itemAssociation;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogInformation("Indexing started!");
            await indexingService.IndexAll();

            // Once the whole tree is indexed, group files into Items. Runs here (not per-file)
            // because grouping needs the folder's full set of siblings.
            logger.LogInformation("Associating items...");
            var result = await itemAssociation.AssociateAllAsync();
            logger.LogInformation("Indexing finished! ({Items} items, {Sidecars} sidecars, {Companions} companions, {Promotions} promoted)",
                result.Items, result.Sidecars, result.Companions, result.Promotions);
        }
    }
}
