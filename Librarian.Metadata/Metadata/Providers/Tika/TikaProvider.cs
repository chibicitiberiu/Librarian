using Librarian.Model;
using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Providers.Tika
{
    /// <summary>
    /// Raw metadata provider backed by an Apache Tika server. Emits each Tika key as a raw
    /// record, deriving the namespace from the key's schema prefix (e.g. "dc:title" becomes
    /// namespace "dc", key "title"). Embedded resources (e.g. archive entries) become
    /// sub-resources. Normalization to canonical attributes is done by the MetadataNormalizer.
    /// </summary>
    public class TikaProvider : IRawMetadataProvider
    {
        private static readonly Guid providerId = new("b7e2a9c4-1d83-4f6e-9a05-3c8d7e21f4b6");

        private readonly TikaService tikaService;
        private readonly ILogger logger;

        public Guid ProviderId => providerId;

        public string DisplayName => "Apache Tika";

        public TikaProvider(TikaService tikaService, ILogger<TikaProvider> logger)
        {
            this.tikaService = tikaService;
            this.logger = logger;
        }

        public async Task<RawMetadataResult> GetRawMetadataAsync(string filePath)
        {
            var result = new RawMetadataResult();

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
            CollectItems(result, resources[0], subResource: null);
            result.Content = resources[0].Content;

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
                CollectItems(result, resource, subResource);
            }

            return result;
        }

        private static void CollectItems(RawMetadataResult result, TikaResource resource, SubResource? subResource)
        {
            foreach (var (key, values) in resource.Metadata)
            {
                // Skip Tika's internal/bookkeeping keys (content, parse stats, digests, ...).
                if (key.StartsWith("X-TIKA:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (@namespace, name) = SplitKey(key);

                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(@namespace, name, value, subResource);
                }
            }
        }

        /// <summary>
        /// Splits a Tika key into (namespace, key) on its schema prefix, e.g. "dc:title" =>
        /// ("dc", "title"). Keys without a prefix go under the generic "tika" namespace.
        /// </summary>
        private static (string Namespace, string Key) SplitKey(string key)
        {
            int separator = key.IndexOf(':');
            if (separator > 0 && separator < key.Length - 1)
                return (key[..separator], key[(separator + 1)..]);

            return ("tika", key);
        }
    }
}
