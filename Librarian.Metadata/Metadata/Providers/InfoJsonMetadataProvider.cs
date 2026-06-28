using System.Globalization;
using System.Text.Json;
using Librarian.Model.MetadataAttributes;

namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// Reads a yt-dlp "&lt;name&gt;.info.json" sidecar next to a video and promotes its useful fields
    /// (title, description, source URL, channel/uploader, upload date, tags, categories) onto the VIDEO
    /// as curated canonical metadata. The 600+ raw json keys stay in the raw layer on the .json file
    /// itself — we never strip raw; this only surfaces the handful worth cataloguing, on the right item.
    /// </summary>
    public class InfoJsonMetadataProvider : IMetadataProvider
    {
        private static readonly Guid providerId = new("a3d9b1c7-6e42-4f08-9b5a-1c7e2d4f8a90");

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".mpg", ".mpeg", ".webm", ".flv", ".ts",
        };

        // yt-dlp tag/category lists can be long; cap so a single video can't flood the canonical layer.
        private const int MaxTags = 30;
        private const int MaxCategories = 10;

        private readonly MetadataFactory metadataFactory;

        public Guid ProviderId => providerId;
        public string DisplayName => "yt-dlp info.json";

        public InfoJsonMetadataProvider(MetadataFactory metadataFactory) => this.metadataFactory = metadataFactory;

        public async Task<MetadataCollection> GetMetadataAsync(string filePath)
        {
            var result = new MetadataCollection();
            if (!File.Exists(filePath) || !VideoExtensions.Contains(Path.GetExtension(filePath)))
                return result;

            string sidecar = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty,
                                          Path.GetFileNameWithoutExtension(filePath) + ".info.json");
            if (!File.Exists(sidecar))
                return result;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(await File.ReadAllTextAsync(sidecar)); }
            catch { return result; }   // a malformed sidecar must not break indexing
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return result;

                AddText(result, root, "title", General.Title);
                AddText(result, root, "description", General.Description);
                AddText(result, root, "webpage_url", General.SourceURL);
                // uploader is the channel/author; fall back to "channel".
                if (!AddText(result, root, "uploader", General.Uploader))
                    AddText(result, root, "channel", General.Uploader);

                if (Str(root, "upload_date") is { Length: 8 } ymd
                    && DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    result.Add(metadataFactory.Create(General.DateReleased, new DateTimeOffset(date, TimeSpan.Zero), ProviderId, editable: true));

                AddArray(result, root, "tags", General.Tag, MaxTags);
                AddArray(result, root, "categories", Media.Genre, MaxCategories);
            }
            return result;
        }

        private bool AddText(MetadataCollection result, JsonElement root, string key, int definitionId)
        {
            if (Str(root, key) is { Length: > 0 } value)
            {
                result.Add(metadataFactory.Create(definitionId, value, ProviderId, editable: true));
                return true;
            }
            return false;
        }

        private void AddArray(MetadataCollection result, JsonElement root, string key, int definitionId, int max)
        {
            if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            int n = 0;
            foreach (var item in arr.EnumerateArray())
            {
                if (n >= max) break;
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } v)
                {
                    result.Add(metadataFactory.Create(definitionId, v, ProviderId, editable: true));
                    n++;
                }
            }
        }

        private static string? Str(JsonElement root, string key)
            => root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata) => throw new NotImplementedException();
    }
}
