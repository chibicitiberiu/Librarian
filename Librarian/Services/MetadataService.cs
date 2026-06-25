using Librarian.DB;
using Librarian.Metadata;
using Librarian.Metadata.Normalization;
using Librarian.Metadata.Providers;
using Librarian.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Services
{
    public class MetadataService
    {
        private readonly Dictionary<Guid, IMetadataProvider> metadataProviders = new();
        private readonly IReadOnlyList<IRawMetadataProvider> rawProviders;
        private readonly MetadataNormalizer normalizer;
        private readonly ILogger logger;
        private readonly MetadataSerializer serializer;
        private readonly FileService fileService;
        private readonly DatabaseContext dbContext;
        private readonly SearchVectorService searchVectors;

        public MetadataService(ILogger<MetadataService> logger,
                               IEnumerable<IMetadataProvider> providers,
                               IEnumerable<IRawMetadataProvider> rawProviders,
                               MetadataNormalizer normalizer,
                               MetadataSerializer serializer,
                               FileService fileService,
                               DatabaseContext dbContext,
                               SearchVectorService searchVectors)
        {
            this.logger = logger;
            this.serializer = serializer;

            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);

            this.rawProviders = rawProviders.ToList();
            this.normalizer = normalizer;
            this.fileService = fileService;
            this.dbContext = dbContext;
            this.searchVectors = searchVectors;
        }

        #region Updating, collecting metadata

        /// <summary>
        /// Collects metadata for a file and stores it: canonical providers and the .meta
        /// sidecar are merged into the canonical tables, while raw providers populate the
        /// raw layer and are promoted to canonical attributes through the normalizer.
        /// </summary>
        public async Task UpdateMetadata(IndexedFile indexedFile)
        {
            // Idempotent rebuild: drop this file's previously-extracted canonical attributes before
            // re-adding, so re-indexing replaces rather than appends (RC-6). Association-promoted
            // attributes are owned by the association pass, so they are preserved.
            RemoveDerivedCanonical(indexedFile.Id);

            // Canonical providers (file attributes, media) and the .meta sidecar.
            await foreach (var attribute in CollectCanonicalAsync(indexedFile))
                StoreCanonical(attribute);
            await dbContext.SaveChangesAsync();

            // Raw providers (Tika): persist the raw layer and promote it to canonical.
            await UpdateRawMetadata(indexedFile);

            // Refresh the FTS vectors for the content + text attributes just written.
            await searchVectors.UpdateFileVectorsAsync(indexedFile.Id);
        }

        private async Task UpdateRawMetadata(IndexedFile indexedFile)
        {
            string filePath = fileService.Resolve(indexedFile.Path);
            string? content = null;

            // Replace this file's raw layer.
            dbContext.RawMetadataAttributes.RemoveRange(
                dbContext.RawMetadataAttributes.Where(r => r.FileId == indexedFile.Id));

            // De-duplicates promoted attributes by (field, sub-resource, value): several raw keys can
            // map to the same canonical field+value (e.g. albumartist spelt three ways), and we want
            // one row per (field, value) on each sub-resource, not one per source key.
            var promotedKeys = new HashSet<(int Def, SubResource? Sub, string Value)>();

            foreach (var provider in rawProviders)
            {
                string providerId = provider.ProviderId.ToString();

                // Replace the canonical attributes previously promoted from this provider.
                RemoveCanonicalForProvider(indexedFile.Id, providerId);

                RawMetadataResult? result = null;
                try
                {
                    result = await provider.GetRawMetadataAsync(filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collecting raw data from provider {providerName} failed", provider.DisplayName);
                }
                if (result is null)
                    continue;

                if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(result.Content))
                    content = StripNulls(result.Content);

                foreach (var item in result.Items)
                {
                    SubResource? subResource = item.SubResource;
                    if (subResource is not null)
                    {
                        subResource.File = indexedFile;
                        subResource = AddOrUpdate(subResource, dbContext.SubResources);
                    }

                    // Postgres text columns reject the NUL byte (0x00); extractors occasionally emit it
                    // from binary-ish content, which would otherwise abort the whole file's save.
                    string value = StripNulls(item.Value);

                    dbContext.RawMetadataAttributes.Add(new RawMetadataAttribute
                    {
                        File = indexedFile,
                        SubResource = subResource,
                        Namespace = item.Namespace,
                        Key = item.Key,
                        Value = value,
                        ProviderId = providerId
                    });

                    foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, value, provider.ProviderId, subResource))
                    {
                        if (!promotedKeys.Add((canonical.AttributeDefinitionId, subResource, CanonicalValueKey(canonical))))
                            continue;
                        canonical.File = indexedFile;
                        StoreNormalized(canonical);
                    }
                }
            }

            StoreContent(indexedFile, content);
            await dbContext.SaveChangesAsync();
        }

        /// <summary>Stores (or clears) the file's extracted text content.</summary>
        private void StoreContent(IndexedFile indexedFile, string? content)
        {
            var contents = dbContext.IndexedFileContents.FirstOrDefault(c => c.FileId == indexedFile.Id);

            if (string.IsNullOrWhiteSpace(content))
            {
                if (contents is not null)
                    dbContext.IndexedFileContents.Remove(contents);
                return;
            }

            if (contents is null)
            {
                contents = new IndexedFileContents { FileId = indexedFile.Id };
                dbContext.IndexedFileContents.Add(contents);
            }

            contents.Content = content;
        }

        /// <summary>Removes a file's extracted canonical attributes (every provider) ahead of an
        /// idempotent rebuild, but keeps association-promoted ones — those are regenerated by the
        /// association pass, not the indexer, and must survive an incremental re-index.</summary>
        private void RemoveDerivedCanonical(int fileId)
        {
            string promo = ItemAssociationService.PromotionProvider.ToString();
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.ProviderId != promo));
        }

        private void RemoveCanonicalForProvider(int fileId, string providerId)
        {
            dbContext.TextAttributes.RemoveRange(dbContext.TextAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.IntegerAttributes.RemoveRange(dbContext.IntegerAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.FloatAttributes.RemoveRange(dbContext.FloatAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.DateAttributes.RemoveRange(dbContext.DateAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
            dbContext.BlobAttributes.RemoveRange(dbContext.BlobAttributes.Where(a => a.FileId == fileId && a.ProviderId == providerId));
        }

        /// <summary>Adds a canonical-provider attribute. The file's prior canonical rows were already
        /// cleared (see <see cref="RemoveDerivedCanonical"/>), so this is a plain, idempotent add.</summary>
        private void StoreCanonical(AttributeBase attribute)
        {
            if (attribute.SubResource is not null)
                attribute.SubResource = AddOrUpdate(attribute.SubResource, dbContext.SubResources);

            StoreNormalized(attribute);
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
        private static string? StripNulls(string? s) =>
            s is null || s.IndexOf('\0') < 0 ? s : s.Replace("\0", string.Empty);

        /// <summary>A stable string form of an attribute's value, for de-duplicating promotions.</summary>
        internal static string CanonicalValueKey(AttributeBase attribute) => attribute switch
        {
            TextAttribute a => a.Value,
            IntegerAttribute a => a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FloatAttribute a => a.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateAttribute a => a.Value.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            BlobAttribute a => Convert.ToBase64String(a.Value),
            _ => string.Empty,
        };

        /// <summary>Adds a freshly-promoted attribute (the file's old ones were already removed).</summary>
        private void StoreNormalized(AttributeBase attribute)
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

        private static SubResource AddOrUpdate(SubResource subResource, DbSet<SubResource> dbSet)
        {
            var existingSubResource = dbSet
                .Where(x => x.File == subResource.File
                                        && x.InternalId == subResource.InternalId
                                        && x.Kind == subResource.Kind
                                        && x.Name == subResource.Name)
                .FirstOrDefault();

            if (existingSubResource is not null)
                return existingSubResource;

            dbSet.Add(subResource);
            return subResource;
        }

        /// <summary>Canonical providers and the .meta sidecar for an indexed file (sets the file reference).</summary>
        private async IAsyncEnumerable<AttributeBase> CollectCanonicalAsync(IndexedFile file)
        {
            string filePath = fileService.Resolve(file.Path);

            await foreach (var attribute in CollectCanonicalAsync(filePath))
            {
                attribute.File = file;
                if (attribute.SubResource != null)
                    attribute.SubResource.File = file;
                yield return attribute;
            }
        }

        /// <summary>Canonical providers and the .meta sidecar (no raw providers, no persistence).</summary>
        private async IAsyncEnumerable<AttributeBase> CollectCanonicalAsync(string filePath)
        {
            foreach (var provider in metadataProviders.Values)
            {
                MetadataCollection? metadataCollection = null;
                try
                {
                    metadataCollection = await provider.GetMetadataAsync(filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collecting data from provider {providerName} failed", provider.DisplayName);
                }

                if (metadataCollection != null)
                {
                    foreach (var attribute in metadataCollection.Attributes)
                        yield return attribute;
                }
            }

            // Fetch metadata from .meta file
            foreach (var attribute in await LoadMetaFile(filePath))
                yield return attribute;
        }

        /// <summary>
        /// Collects all canonical metadata for display: canonical providers, the .meta
        /// sidecar, and raw providers promoted through the normalizer. Does not persist.
        /// </summary>
        public async IAsyncEnumerable<AttributeBase> CollectMetadataAsync(string filePath)
        {
            await foreach (var attribute in CollectCanonicalAsync(filePath))
                yield return attribute;

            foreach (var provider in rawProviders)
            {
                RawMetadataResult? result = null;
                try
                {
                    result = await provider.GetRawMetadataAsync(filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collecting raw data from provider {providerName} failed", provider.DisplayName);
                }
                if (result is null)
                    continue;

                foreach (var item in result.Items)
                {
                    foreach (var canonical in normalizer.NormalizeAll(item.Namespace, item.Key, item.Value, provider.ProviderId, item.SubResource))
                    {
                        // Populate the definition reference so the view can show group/name.
                        canonical.AttributeDefinition ??= dbContext.AttributeDefinitions.Find(canonical.AttributeDefinitionId)!;
                        yield return canonical;
                    }
                }
            }
        }

        #endregion

        private async Task<IEnumerable<AttributeBase>> LoadMetaFile(string fileName)
        {
            var metaFile = GetMetaFile(fileName);
            if (File.Exists(metaFile))
                return await serializer.Deserialize(metaFile);

            return Enumerable.Empty<AttributeBase>();
        }

        private async Task SaveMetaFile(string fileName, IEnumerable<AttributeBase> fields)
        {
            //fields = fields.Where(x => x.CanSaveToFile);
            await serializer.Serialize(GetMetaFile(fileName), fields);
        }

        private static string GetMetaFile(string fileName)
        {
            return fileName + ".meta";
        }

        public bool IsMetaFile(string fileName)
        {
            return fileName.EndsWith(".meta");
        }
    }
}
