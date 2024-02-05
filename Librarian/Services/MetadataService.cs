using Librarian.Metadata;
using Librarian.Metadata.Providers;
using Librarian.Model;
using Microsoft.Extensions.Configuration;
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
        private bool useMetaFiles;
        private readonly MetadataSerializer serializer;

        public MetadataService(ILogger<MetadataService> logger,
                               IEnumerable<IMetadataProvider> providers,
                               IConfiguration configuration,
                               MetadataSerializer serializer)
        {
            this.logger = logger;
            this.serializer = serializer;

            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);

            LoadConfiguration(configuration);
        }

        private void LoadConfiguration(IConfiguration configuration)
        {
            if (!bool.TryParse(configuration["UseMetaFiles"], out useMetaFiles))
                useMetaFiles = true;
        }

        public async Task<IEnumerable<MetadataAttributeBase>> GetMetadataAsync(string fileName, bool forceUpdate)
        {
            IEnumerable<MetadataAttributeBase> result = Enumerable.Empty<MetadataAttributeBase>();

            foreach (var provider in metadataProviders.Values)
            {
                var metadataFields = await provider.GetMetadataAsync(fileName);
                if (metadataFields != null)
                {
                    result = result.Concat(metadataFields.Metadata);
                }
            }

            // Save to meta file
            if (useMetaFiles)
                await SaveMetaFile(fileName, result);

            return result;
        }

        private async Task SaveMetaFile(string fileName, IEnumerable<MetadataAttributeBase> fields)
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
        
        internal Task UpdateMetadata(DirectoryInfo directory)
        {
            throw new NotImplementedException();
        }

        internal Task UpdateMetadata(IndexedFile indexedFile)
        {
            throw new NotImplementedException();
        }
    }
}
