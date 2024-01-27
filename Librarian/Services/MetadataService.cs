using Librarian.Metadata;
using Librarian.Metadata.Providers;
using Librarian.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Librarian.Services
{
    public class MetadataService
    {
        private readonly Dictionary<Guid, IMetadataProvider> metadataProviders = new();
        private readonly ILogger logger;

        public MetadataService(ILogger<MetadataService> logger, IEnumerable<IMetadataProvider> providers)
        {
            this.logger = logger;
            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);
        }

        public async Task<IEnumerable<MetadataBase>> GetMetadataAsync(string fileName)
        {
            IEnumerable<MetadataBase> result = Enumerable.Empty<MetadataBase>();

            foreach (var provider in metadataProviders.Values)
            {
                var metadataFields = await provider.GetMetadataAsync(fileName);
                if (metadataFields != null)
                {
                    result = result.Concat(metadataFields.Metadata);
                }
            }

            return result;
        }
    }
}
