using Librarian.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Providers.Tika
{
    /// <summary>
    /// Raw metadata provider backed by an Apache Tika server. Emits each Tika key as a raw
    /// record, deriving the namespace from the key's schema prefix (e.g. "dc:title" becomes
    /// namespace "dc", key "title"). Files embedded within the document (archive entries, ...) are
    /// reported as <see cref="EmbeddedResource"/>s, which the catalog explodes into their own virtual
    /// files when the container is an archive (collection_plan.md §7). Normalization to canonical
    /// attributes is done by the MetadataNormalizer.
    /// </summary>
    public class TikaProvider : IRawMetadataProvider
    {
        private static readonly Guid providerId = new("b7e2a9c4-1d83-4f6e-9a05-3c8d7e21f4b6");

        private readonly TikaService tikaService;
        private readonly ILogger logger;
        private readonly HashSet<string> skipExtensions;

        // Tika parses a whole video/audio container to extract little that meta-cli (libav) and exiftool
        // don't already get faster — and it's the dominant per-file cost when enabled. Skip those formats.
        // Overridable via the "Tika:SkipExtensions" config (config.d).
        private static readonly string[] DefaultSkipExtensions =
        {
            ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".webm", ".wmv", ".flv", ".mpg", ".mpeg",
            ".m2ts", ".mts", ".ts", ".vob", ".ogv", ".3gp", ".divx", ".rm", ".rmvb", ".asf",
            ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav", ".wma", ".alac", ".ape", ".wv",
        };

        public Guid ProviderId => providerId;

        public string DisplayName => "Apache Tika";

        public TikaProvider(TikaService tikaService, ILogger<TikaProvider> logger, IConfiguration? configuration = null)
        {
            this.tikaService = tikaService;
            this.logger = logger;

            var configured = configuration?.GetSection("Tika:SkipExtensions").GetChildren()
                .Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();
            skipExtensions = new HashSet<string>(
                configured is { Length: > 0 } ? configured : DefaultSkipExtensions,
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<RawMetadataResult> GetRawMetadataAsync(string filePath)
        {
            var result = new RawMetadataResult();

            // Tika handles files, not directories.
            if (Directory.Exists(filePath))
                return result;

            // Skip formats where Tika is slow and adds little over the media providers (video/audio).
            if (skipExtensions.Contains(Path.GetExtension(filePath)))
                return result;

            IReadOnlyList<TikaResource>? resources;
            try
            {
                resources = await tikaService.GetMetadataAsync(filePath);
            }
            catch (TransientMetadataException)
            {
                // Let the provider-execution policy retry, and mark the file incomplete if it persists.
                throw;
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

            // Any further resources are files embedded within it (e.g. archive entries). Report each as a
            // first-class embedded resource; the catalog materializes archive entries as their own virtual
            // files (no per-entry cap — that cap only existed to stop entry metadata flooding the archive's
            // raw layer, which no longer happens). collection_plan.md §7.
            for (int i = 1; i < resources.Count; i++)
            {
                var resource = resources[i];
                var embedded = new EmbeddedResource(resource.EmbeddedPath ?? $"Embedded resource {i}")
                {
                    Content = resource.Content,
                    Size = resource.Size,
                };
                CollectEmbeddedItems(embedded, resource);
                result.AddEmbedded(embedded);
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

        private static void CollectEmbeddedItems(EmbeddedResource embedded, TikaResource resource)
        {
            foreach (var (key, values) in resource.Metadata)
            {
                if (key.StartsWith("X-TIKA:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var (@namespace, name) = SplitKey(key);

                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        embedded.Add(@namespace, name, value);
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
