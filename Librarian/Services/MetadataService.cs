using Librarian.DB;
using Librarian.Metadata;
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
        private readonly ILogger logger;
        private readonly MetadataSerializer serializer;
        private readonly FileService fileService;
        private readonly DatabaseContext dbContext;

        public MetadataService(ILogger<MetadataService> logger,
                               IEnumerable<IMetadataProvider> providers,
                               MetadataSerializer serializer,
                               FileService fileService,
                               DatabaseContext dbContext)
        {
            this.logger = logger;
            this.serializer = serializer;

            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);
            this.fileService = fileService;
            this.dbContext = dbContext;
        }

        #region Updating, collecting metadata

        /// <summary>
        /// Collects metadata and updates index
        /// </summary>
        /// <param name="indexedFile">File to update for</param>
        /// <returns></returns>
        public async Task UpdateMetadata(IndexedFile indexedFile)
        {
            await foreach (var attribute in CollectMetadataAsync(indexedFile))
            {
                if (attribute.SubResource is not null)
                    attribute.SubResource = AddOrUpdate(attribute.SubResource, dbContext.SubResources);

                if (attribute is BlobAttribute blobAttribute)
                    AddOrUpdate(blobAttribute, dbContext.BlobAttributes);
                else if (attribute is DateAttribute dateAttribute)
                    AddOrUpdate(dateAttribute, dbContext.DateAttributes);
                else if (attribute is FloatAttribute floatAttribute)
                    AddOrUpdate(floatAttribute, dbContext.FloatAttributes);
                else if (attribute is IntegerAttribute intAttribute)
                    AddOrUpdate(intAttribute, dbContext.IntegerAttributes);
                else if (attribute is TextAttribute textAttribute)
                    AddOrUpdate(textAttribute, dbContext.TextAttributes);
            }

            await dbContext.SaveChangesAsync();
        }

        private static void AddOrUpdate<TAttribute>(TAttribute attribute, DbSet<TAttribute> dbSet) where TAttribute : AttributeBase
        {
            var existingAttribute = dbSet
                .Where(x => x.AttributeDefinition == attribute.AttributeDefinition
                                        && x.File == attribute.File
                                        && x.SubResource == attribute.SubResource
                                        && x.ProviderId == attribute.ProviderId)
                .FirstOrDefault();

            if (existingAttribute is not null)
                existingAttribute.Update(attribute);
            else
                dbSet.Add(attribute);
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
            {
                return existingSubResource;
            }
            else
            {
                dbSet.Add(subResource);
                return subResource;
            }
        }

        /// <summary>
        /// Collects metadata for given file from providers and .meta file.
        /// Does not store the metadata in the database.
        /// </summary>
        /// <param name="file">File to collect for</param>
        /// <returns></returns>
        private async IAsyncEnumerable<AttributeBase> CollectMetadataAsync(IndexedFile file)
        {
            string filePath = fileService.Resolve(file.Path);

            // Fetch metadata from providers
            await foreach (var attribute in CollectMetadataAsync(filePath))
            {
                attribute.File = file;
                if (attribute.SubResource != null)
                    attribute.SubResource.File = file;
                yield return attribute;
            }
        }

        /// <summary>
        /// Collects metadata for given file from providers and .meta file.
        /// Does not store the metadata in the database.
        /// </summary>
        /// <param name="filePath">Path to file to collect for</param>
        /// <returns></returns>
        public async IAsyncEnumerable<AttributeBase> CollectMetadataAsync(string filePath)
        {
            // Fetch metadata from providers
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
