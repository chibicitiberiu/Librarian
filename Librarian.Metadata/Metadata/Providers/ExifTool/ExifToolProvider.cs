using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Providers.ExifTool
{
    /// <summary>
    /// Raw metadata provider backed by the external <c>exiftool</c> binary. It <b>augments</b> Tika
    /// (it does not replace it): both run side by side, each emitting raw namespaced records, and the
    /// <see cref="Normalization.MetadataNormalizer"/> promotes the useful keys. ExifTool contributes
    /// deeper embedded tags than Tika's image parsers — maker notes, IPTC, XMP, GPS, RAW formats.
    /// Each exiftool "Group0:Tag" key becomes a raw record whose namespace is the group (e.g.
    /// "EXIF:Make" -> namespace "exif", key "Make").
    /// </summary>
    public class ExifToolProvider : IRawMetadataProvider
    {
        private static readonly Guid providerId = new("2f8e4d7a-1b3c-4e8f-9a2d-6c5b8e4f7a1b");

        // Family-0 group that is only exiftool's own bookkeeping (version, warnings) — never useful.
        private const string ToolNamespace = "exiftool";

        // Tags that merely restate filesystem facts already captured by FileMetadataProvider, or are
        // pure noise. Filtered out so they don't bloat the raw layer. (File:ImageWidth/ImageHeight,
        // File:MIMEType, File:FileType are kept — they carry real, cross-format image facts.)
        private static readonly HashSet<string> SkippedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "SourceFile", "Directory", "FileName", "FilePath", "FilePermissions",
            "FileModifyDate", "FileAccessDate", "FileInodeChangeDate", "FileTypeExtension",
        };

        private readonly ExifToolService exifToolService;
        private readonly ILogger logger;

        public Guid ProviderId => providerId;

        public string DisplayName => "ExifTool";

        public ExifToolProvider(ExifToolService exifToolService, ILogger<ExifToolProvider> logger)
        {
            this.exifToolService = exifToolService;
            this.logger = logger;
        }

        public async Task<RawMetadataResult> GetRawMetadataAsync(string filePath)
        {
            IReadOnlyDictionary<string, string>? tags;
            try
            {
                tags = await exifToolService.GetMetadataAsync(filePath);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Could not retrieve exiftool metadata for file {file}", filePath);
                return new RawMetadataResult();
            }

            return BuildRawResult(tags);
        }

        /// <summary>
        /// Turns exiftool's flat "Group0:Tag" -> value map into raw namespaced records, dropping
        /// bookkeeping/filesystem-duplicate keys and binary-blob placeholders. Public + static so it
        /// is unit-testable without invoking the binary.
        /// </summary>
        public static RawMetadataResult BuildRawResult(IReadOnlyDictionary<string, string>? tags)
        {
            var result = new RawMetadataResult();
            if (tags is null)
                return result;

            foreach (var (rawKey, value) in tags)
            {
                var (@namespace, name) = SplitKey(rawKey);
                if (@namespace == ToolNamespace || SkippedKeys.Contains(name))
                    continue;

                // With -json (no -b), exiftool renders binary tags (thumbnails, previews, ICC blobs)
                // as a "(Binary data N bytes, use -b option to extract)" placeholder — skip those.
                if (value.StartsWith("(Binary data", StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(@namespace, name, value);
            }

            return result;
        }

        /// <summary>Splits "EXIF:Make" into ("exif", "Make"); ungrouped keys go under "exiftool".</summary>
        private static (string Namespace, string Key) SplitKey(string key)
        {
            int separator = key.IndexOf(':');
            if (separator > 0 && separator < key.Length - 1)
                return (key[..separator].ToLowerInvariant(), key[(separator + 1)..]);

            return (ToolNamespace, key);
        }
    }
}
