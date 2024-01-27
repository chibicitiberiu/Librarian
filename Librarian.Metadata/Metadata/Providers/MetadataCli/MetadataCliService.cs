using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace Librarian.Metadata.Providers.MetadataCli
{
    public class MyTest
    {
        public MetadataCliStream[]? Streams { get; set; }

        public MetadataCliChapter[]? Chapters { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> Rest { get; set; }
    }

    public class MetadataCliService
    {
        public string BinaryPath { get; }

        public MetadataCliService(IConfiguration configuration)
        {
            BinaryPath = configuration["MetadataCliPath"]!;
        }

        public async Task<MetadataCliResult?> GetMetadataAsync(string fileName)
        {
            var (exitCode, output, error) = await ProcessHelper.RunProcessAsync(BinaryPath, "get", fileName);
            if (exitCode != 0)
                throw new Exception("Failed to retrieve metadata.\n" + error);

            var dict2 = JsonConvert.DeserializeObject<MyTest>(output);


            return JsonConvert.DeserializeObject<MetadataCliResult>(output);
        }
    }
}