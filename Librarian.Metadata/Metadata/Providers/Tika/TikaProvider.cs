using Librarian.Model;
using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Providers.Tika
{
    /// <summary>
    /// Metadata provider backed by an Apache Tika server. Extracts metadata from a
    /// broad range of document, image and archive formats. Content text is also
    /// returned by Tika and will be stored once the content-indexing pipeline lands.
    /// </summary>
    public class TikaProvider : IMetadataProvider
    {
        private static readonly Guid providerId = new("b7e2a9c4-1d83-4f6e-9a05-3c8d7e21f4b6");

        private readonly TikaService tikaService;
        private readonly MetadataFactory metadataFactory;
        private readonly ILogger logger;

        public Guid ProviderId => providerId;

        public string DisplayName => "Apache Tika";

        public TikaProvider(TikaService tikaService, MetadataFactory metadataFactory, ILogger<TikaProvider> logger)
        {
            this.tikaService = tikaService;
            this.metadataFactory = metadataFactory;
            this.logger = logger;
        }

        public async Task<MetadataCollection> GetMetadataAsync(string filePath)
        {
            var result = new MetadataCollection();

            // Tika handles files, not directories.
            if (Directory.Exists(filePath))
                return result;

            IReadOnlyList<TikaResource>? resources;
            try
            {
                resources = await tikaService.GetMetadataAsync(filePath);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Could not retrieve Tika metadata for file {file}", filePath);
                return result;
            }

            if (resources is null || resources.Count == 0)
                return result;

            // The first resource is the top-level document.
            CollectAttributes(result, resources[0], subResource: null);

            // Any further resources are files embedded within it (e.g. archive entries).
            for (int i = 1; i < resources.Count; i++)
            {
                var resource = resources[i];
                var subResource = new SubResource
                {
                    Kind = SubResourceKind.EmbeddedFile,
                    Name = resource.EmbeddedPath ?? $"Embedded resource {i}"
                };
                result.AddSubResource(subResource);
                CollectAttributes(result, resource, subResource);
            }

            return result;
        }

        private void CollectAttributes(MetadataCollection result, TikaResource resource, SubResource? subResource)
        {
            foreach (var (key, values) in resource.Metadata)
            {
                // Skip Tika's internal/bookkeeping keys (content, parse stats, digests, ...).
                if (key.StartsWith("X-TIKA:", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    try
                    {
                        var attribute = metadataFactory.Create(key, value, ProviderId, providerAttributeId: key, editable: true, subResource: subResource);
                        result.Add(attribute);
                    }
                    catch (Exception ex)
                    {
                        // A single unparseable value must not lose the rest of the file's metadata.
                        logger.LogTrace(ex, "Could not map Tika attribute {key}={value} for a file", key, value);
                    }
                }
            }
        }

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
