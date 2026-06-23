using Microsoft.Extensions.Configuration;
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

        public string BinaryPath { get; }

        public MetadataCliService(IConfiguration configuration)
        {
            BinaryPath = configuration["MetadataCliPath"]!;
        }

        public async Task<MetadataCliResult?> GetMetadataAsync(string fileName)
        {
            // directories not supported
            if (Directory.Exists(fileName))
                return null;

            var (exitCode, output, error) = await ProcessHelper.RunProcessAsync(BinaryPath, "get", fileName);
            if (exitCode != 0)
                throw new Exception("Failed to retrieve metadata.\n" + error);

            return JsonConvert.DeserializeObject<MetadataCliResult>(output, serializerSettings);
        }
    }
}