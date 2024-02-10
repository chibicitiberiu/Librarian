using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace Librarian.Metadata.Providers.MetadataCli
{
    public class MetadataCliService
    {
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

            return JsonConvert.DeserializeObject<MetadataCliResult>(output);
        }
    }
}