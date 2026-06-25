using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Librarian.Metadata.Providers.ExifTool
{
    /// <summary>
    /// Thin wrapper around Phil Harvey's external <c>exiftool</c> binary. Runs it once per file with
    /// grouped JSON output and returns the tag dictionary keyed by exiftool's "Group0:Tag" form
    /// (e.g. "EXIF:DateTimeOriginal"). Like meta-cli, its absence is non-fatal — Tika still provides
    /// metadata — so a missing binary is warned once and then skipped.
    /// </summary>
    public class ExifToolService
    {
        public string BinaryPath { get; }

        private readonly ILogger<ExifToolService> logger;
        private bool warnedUnavailable;

        public ExifToolService(IConfiguration configuration, ILogger<ExifToolService> logger)
        {
            // Defaults to resolving "exiftool" on PATH; override with a full path via config.
            BinaryPath = configuration["ExifToolPath"] is { Length: > 0 } path ? path : "exiftool";
            this.logger = logger;
        }

        /// <summary>
        /// Returns the file's tags as (groupedKey -> value) — keys are exiftool's "Group0:Tag" form
        /// (e.g. "EXIF:Make") — or null when exiftool is unavailable or produced nothing usable.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, string>?> GetMetadataAsync(string filePath)
        {
            // exiftool reads files, not directories.
            if (Directory.Exists(filePath))
                return null;

            int exitCode;
            string output;
            string error;
            try
            {
                // -json    machine-readable; -G0 prefixes each tag with its family-0 group (-> namespace);
                // -n       numeric values (no friendly suffixes) so they coerce cleanly — dates keep
                //          exiftool's "yyyy:MM:dd HH:mm:ss" form, which ValueCoercer.ExifDate parses;
                // -charset utf8 for non-ASCII tags; -- ends options before the path.
                (exitCode, output, error) = await ProcessHelper.RunProcessAsync(
                    BinaryPath, "-json", "-G0", "-n", "-charset", "utf8", "--", filePath);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                // Binary not found on PATH / at the configured location.
                if (!warnedUnavailable)
                {
                    warnedUnavailable = true;
                    logger.LogWarning("exiftool not found ('{path}'); skipping it (Tika still provides metadata).", BinaryPath);
                }
                return null;
            }

            // exiftool returns a non-zero exit for unsupported/partly-readable files but still emits
            // whatever it could parse; only treat an empty output as "nothing".
            if (exitCode != 0)
                logger.LogTrace("exiftool exited {code} for {file}: {error}", exitCode, filePath, error);

            return ParseExifToolJson(output);
        }

        /// <summary>
        /// Parses exiftool's JSON output (a one-element array of tag objects) into a flat
        /// (key -> string value) map. Skips nulls and structured (array/object) tags, keeping scalar
        /// tags only. Public + static so the parsing is unit-testable without invoking the binary.
        /// </summary>
        public static IReadOnlyDictionary<string, string>? ParseExifToolJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            JArray array;
            try
            {
                array = JArray.Parse(json);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }

            if (array.Count == 0 || array[0] is not JObject obj)
                return null;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in obj.Properties())
            {
                if (prop.Value.Type is JTokenType.Null or JTokenType.Array or JTokenType.Object)
                    continue;

                var value = prop.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    result[prop.Name] = value;
            }
            return result;
        }
    }
}
