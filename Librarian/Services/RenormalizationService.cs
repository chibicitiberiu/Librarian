using Librarian.DB;
using Librarian.Metadata.Normalization;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Services
{
    /// <summary>
    /// Rebuilds the canonical attributes that were promoted from the raw layer, using the
    /// current normalization rules and the metadata already stored in RawMetadataAttributes.
    /// This is the payoff of keeping a raw layer: after changing the rules, the canonical
    /// metadata can be regenerated without re-reading any files.
    /// </summary>
    public class RenormalizationService
    {
        private readonly DatabaseContext dbContext;
        private readonly MetadataNormalizer normalizer;
        private readonly SearchVectorService searchVectors;
        private readonly ILogger<RenormalizationService> logger;

        public RenormalizationService(DatabaseContext dbContext,
                                      MetadataNormalizer normalizer,
                                      SearchVectorService searchVectors,
                                      ILogger<RenormalizationService> logger)
        {
            this.dbContext = dbContext;
            this.normalizer = normalizer;
            this.searchVectors = searchVectors;
            this.logger = logger;
        }

        /// <summary>Re-promotes every file's raw metadata. Returns the number of canonical attributes produced.</summary>
        public async Task<int> RenormalizeAllAsync()
        {
            var fileIds = await dbContext.RawMetadataAttributes
                .Select(r => r.FileId)
                .Distinct()
                .ToListAsync();

            int produced = 0;
            foreach (var fileId in fileIds)
                produced += await RenormalizeFileAsync(fileId);

            logger.LogInformation("Re-normalized {fileCount} files, producing {attributeCount} canonical attributes.", fileIds.Count, produced);
            return produced;
        }

        private async Task<int> RenormalizeFileAsync(int fileId)
        {
            var rawRows = await dbContext.RawMetadataAttributes
                .Where(r => r.FileId == fileId)
                .ToListAsync();

            // Replace the canonical attributes that were promoted from each raw provider.
            foreach (var providerId in rawRows.Select(r => r.ProviderId).Where(p => p is not null).Distinct())
                RemoveCanonicalForProvider(fileId, providerId!);

            int produced = 0;
            foreach (var raw in rawRows)
            {
                if (!Guid.TryParse(raw.ProviderId, out var providerGuid))
                    continue;

                var canonical = normalizer.Normalize(raw.Namespace, raw.Key, raw.Value, providerGuid);
                if (canonical is null)
                    continue;

                canonical.FileId = raw.FileId;
                canonical.SubResourceId = raw.SubResourceId;
                Store(canonical);
                produced++;
            }

            await dbContext.SaveChangesAsync();

            // The promoted text attributes changed; refresh their search vectors.
            await searchVectors.UpdateFileVectorsAsync(fileId);
            return produced;
        }

        private void RemoveCanonicalForProvider(int fileId, string providerId)
        {
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
        }

        private void Store(AttributeBase attribute)
        {
            switch (attribute)
            {
                case BlobAttribute a: dbContext.BlobAttributes.Add(a); break;
                case DateAttribute a: dbContext.DateAttributes.Add(a); break;
                case FloatAttribute a: dbContext.FloatAttributes.Add(a); break;
                case IntegerAttribute a: dbContext.IntegerAttributes.Add(a); break;
                case TextAttribute a: dbContext.TextAttributes.Add(a); break;
            }
        }
    }
}
