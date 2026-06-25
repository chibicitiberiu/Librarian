using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Librarian.Metadata.Providers.MetadataCli
{
    public class MetadataCliService
    {
        // meta-cli emits snake_case JSON keys (e.g. "sample_rate"); map them onto the
        // PascalCase model properties. Dictionary keys (metadata tags) are left untouched.
        private static readonly JsonSerializerSettings serializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        public string? BinaryPath { get; }

        private readonly ILogger<MetadataCliService> logger;
        private bool warnedUnavailable;

        public MetadataCliService(IConfiguration configuration, ILogger<MetadataCliService> logger)
        {
            BinaryPath = configuration["MetadataCliPath"];
            this.logger = logger;
        }

        public async Task<MetadataCliResult?> GetMetadataAsync(string fileName)
        {
            // directories not supported
            if (Directory.Exists(fileName))
                return null;

            // Skip gracefully (once-warned) when meta-cli isn't installed, rather than throwing per
            // file — Tika still provides metadata, so its absence is non-fatal but worth surfacing.
            if (string.IsNullOrWhiteSpace(BinaryPath) || !File.Exists(BinaryPath))
            {
                if (!warnedUnavailable)
                {
                    warnedUnavailable = true;
                    logger.LogWarning("meta-cli not found at '{path}'; skipping it (Tika still provides metadata).", BinaryPath);
                }
                return null;
            }

            var (exitCode, output, error) = await ProcessHelper.RunProcessAsync(BinaryPath, "get", fileName);
            if (exitCode != 0)
                throw new Exception("Failed to retrieve metadata.\n" + error);

            return JsonConvert.DeserializeObject<MetadataCliResult>(output, serializerSettings);
        }
    }
}