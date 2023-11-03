using Librarian.Metadata;
using Librarian.Metadata.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Librarian.Services
{
    public class MetadataService
    {
        private readonly Dictionary<int, IMetadataProvider> metadataProviders = new();
        private readonly ILogger logger;

        public MetadataService(ILogger<MetadataService> logger, IEnumerable<IMetadataProvider> providers)
        {
            this.logger = logger;
            foreach (var provider in providers)
                metadataProviders.Add(provider.ProviderId, provider);
        }

        public IEnumerable<MetadataField> GetMetadata(string fileName)
        {
            foreach (var provider in metadataProviders.Values)
            {
                var metadataFields = provider.GetMetadata(fileName);
                if (metadataFields != null)
                {
                    foreach (var metadataField in metadataFields)
                    {
                        metadataField.ProviderId = provider.ProviderId;
                        yield return metadataField;
                    }
                }
            }
        }
    }
}
