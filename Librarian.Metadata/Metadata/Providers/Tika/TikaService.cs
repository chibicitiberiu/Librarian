using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;

namespace Librarian.Metadata.Providers.Tika
{
    /// <summary>
    /// A single resource returned by Tika's recursive metadata endpoint: either the
    /// top-level document or an embedded file (e.g. an entry inside an archive).
    /// Metadata values are normalized to string arrays (Tika returns either a single
    /// string or an array for multi-valued keys).
    /// </summary>
    public class TikaResource
    {
        public IReadOnlyDictionary<string, string[]> Metadata { get; }

        public TikaResource(IReadOnlyDictionary<string, string[]> metadata)
        {
            Metadata = metadata;
        }

        /// <summary>Extracted text content of the resource, if any.</summary>
        public string? Content => Get("X-TIKA:content");

        /// <summary>Path of this resource within its container (for embedded resources).</summary>
        public string? EmbeddedPath => Get("X-TIKA:embedded_resource_path");

        public string? ContentType => Get("Content-Type");

        private string? Get(string key) => Metadata.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;
    }

    /// <summary>
    /// Talks to a running Apache Tika server (https://tika.apache.org). Uses the
    /// recursive endpoint (/rmeta/text) so a single request returns the metadata and
    /// extracted text for the file and any resources embedded within it.
    /// </summary>
    public class TikaService
    {
        private const long DefaultMaxFileSize = 1024L * 1024 * 1024; // 1 GiB

        private readonly ILogger logger;
        private readonly HttpClient? httpClient;
        private readonly long maxFileSize;

        /// <summary>True when a Tika server URL is configured.</summary>
        public bool IsEnabled => httpClient is not null;

        public TikaService(IConfiguration configuration, ILogger<TikaService> logger)
        {
            this.logger = logger;

            string? url = configuration["TikaUrl"];
            if (string.IsNullOrWhiteSpace(url))
            {
                logger.LogInformation("TikaUrl is not configured; Tika metadata extraction is disabled.");
            }
            else
            {
                httpClient = new HttpClient
                {
                    BaseAddress = new Uri(url.TrimEnd('/') + "/"),
                    Timeout = TimeSpan.FromMinutes(5)
                };
            }

            if (!long.TryParse(configuration["TikaMaxFileSize"], out maxFileSize) || maxFileSize <= 0)
                maxFileSize = DefaultMaxFileSize;
        }

        /// <summary>
        /// Returns the metadata and content for the given file. The first element is
        /// the top-level document; any further elements are embedded resources.
        /// Returns null when Tika is disabled, the file is missing/too large, or the
        /// server cannot be reached.
        /// </summary>
        public async Task<IReadOnlyList<TikaResource>?> GetMetadataAsync(string filePath)
        {
            if (httpClient is null)
                return null;

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            if (fileInfo.Length > maxFileSize)
            {
                logger.LogTrace("Skipping Tika for {file}: size {size} exceeds limit {limit}", filePath, fileInfo.Length, maxFileSize);
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, "rmeta/text");
            await using var stream = File.OpenRead(filePath);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            // Pass the original file name to help Tika's content detector.
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileInfo.Name };
            request.Content = content;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or System.Net.Sockets.SocketException)
            {
                // Tika is unreachable / restarting / slow (timeout) — a retryable condition.
                throw new TransientMetadataException($"Tika request failed for {fileInfo.Name}", ex);
            }

            using (response)
            {
                int status = (int)response.StatusCode;

                // 5xx, 408 (request timeout) and 429 (too many requests) are transient server-side
                // conditions worth retrying; other 4xx (e.g. unsupported media) are permanent — the
                // file simply has no Tika metadata, which is not an "incomplete extraction".
                if (status >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || status == 429)
                    throw new TransientMetadataException($"Tika returned {status} for {fileInfo.Name}");

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogTrace("Tika returned {status} for {file}; no metadata.", status, filePath);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                return Parse(json);
            }
        }

        private static IReadOnlyList<TikaResource> Parse(string json)
        {
            var result = new List<TikaResource>();

            foreach (var element in JArray.Parse(json).OfType<JObject>())
            {
                var metadata = new Dictionary<string, string[]>();
                foreach (var property in element.Properties())
                    metadata[property.Name] = ToStringArray(property.Value);

                result.Add(new TikaResource(metadata));
            }

            return result;
        }

        private static string[] ToStringArray(JToken token)
        {
            if (token is JArray array)
                return array.Select(x => x.ToString()).ToArray();

            return new[] { token.ToString() };
        }
    }
}
