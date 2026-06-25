using System.Globalization;
using CsvHelper;
using Librarian.Data;
using Librarian.Metadata.Normalization;
using Librarian.Metadata.Providers;
using Librarian.Metadata.Providers.ExifTool;
using Librarian.Metadata.Providers.MetadataCli;
using Librarian.Metadata.Providers.Tika;
using Librarian.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MetadataCollector
{
    /// <summary>
    /// Runs the real raw providers (Tika, ExifTool) + meta-cli over each file and streams the results to
    /// CSV. The raw layer and the normalizer are pure (no database); the vocabulary and aliases are read
    /// from the embedded CSVs so canonical promotion and the "Mapped" flag work standalone.
    /// </summary>
    internal sealed class Collector : IDisposable
    {
        private const int MaxValueLength = 500;

        private readonly ILogger logger;
        private readonly TikaProvider tika;
        private readonly ExifToolProvider exifTool;
        private readonly MetadataCliService metaCli;
        private readonly MetadataNormalizer normalizer;

        private readonly Dictionary<int, AttributeDefinition> vocab;
        private readonly Dictionary<string, AttributeAlias> aliases; // keyed by lower-case alias

        private readonly string outputDir;
        private readonly StreamWriter rawOut;
        private readonly CsvWriter rawCsv;
        private readonly StreamWriter canonicalOut;
        private readonly CsvWriter canonicalCsv;

        private readonly List<FileRow> fileRows = new();
        private readonly Dictionary<(string Provider, string Namespace, string Key), UnmappedAgg> unmapped = new();
        private readonly Dictionary<string, long> perProvider = new();
        private long totalRaw, totalCanonical, totalUnmapped;

        private sealed class UnmappedAgg
        {
            public readonly HashSet<string> Files = new();
            public int Occurrences;
            public string Sample = "";
        }

        public Collector(IConfiguration config, ILoggerFactory loggerFactory, string outputDir)
        {
            logger = loggerFactory.CreateLogger<Collector>();

            var tikaService = new TikaService(config, loggerFactory.CreateLogger<TikaService>());
            tika = new TikaProvider(tikaService, loggerFactory.CreateLogger<TikaProvider>());
            var exifService = new ExifToolService(config, loggerFactory.CreateLogger<ExifToolService>());
            exifTool = new ExifToolProvider(exifService, loggerFactory.CreateLogger<ExifToolProvider>());
            metaCli = new MetadataCliService(config, loggerFactory.CreateLogger<MetadataCliService>());
            normalizer = new MetadataNormalizer(loggerFactory.CreateLogger<MetadataNormalizer>());

            vocab = Datasets.GetMetadataAttributes().ToDictionary(d => d.Id);
            aliases = Datasets.GetAliases().ToDictionary(a => a.Alias, StringComparer.OrdinalIgnoreCase);

            this.outputDir = outputDir;
            Directory.CreateDirectory(outputDir);

            rawOut = new StreamWriter(Path.Combine(outputDir, "raw-metadata.csv"));
            rawCsv = new CsvWriter(rawOut, CultureInfo.InvariantCulture);
            rawCsv.WriteHeader<RawRow>();
            rawCsv.NextRecord();

            canonicalOut = new StreamWriter(Path.Combine(outputDir, "canonical-metadata.csv"));
            canonicalCsv = new CsvWriter(canonicalOut, CultureInfo.InvariantCulture);
            canonicalCsv.WriteHeader<CanonicalRow>();
            canonicalCsv.NextRecord();
        }

        public async Task ProcessFileAsync(string path)
        {
            var info = new FileInfo(path);
            var row = new FileRow
            {
                File = path,
                Extension = info.Extension,
                SizeBytes = info.Exists ? info.Length : 0,
                Status = "OK",
            };

            row.TikaItems = await RunRawAsync(tika, "Tika", path, row);
            row.ExifToolItems = await RunRawAsync(exifTool, "ExifTool", path, row);
            row.MetaCliItems = await RunMetaCliAsync(path, row);

            int total = row.TikaItems + row.ExifToolItems + row.MetaCliItems;
            if (total == 0)
                row.Status = row.Error != null ? "Error" : "Empty";

            fileRows.Add(row);
        }

        private async Task<int> RunRawAsync(IRawMetadataProvider provider, string name, string path, FileRow row)
        {
            RawMetadataResult result;
            try
            {
                result = await provider.GetRawMetadataAsync(path);
            }
            catch (Exception ex)
            {
                row.Error ??= $"{name}: {ex.Message}";
                logger.LogDebug(ex, "{provider} failed for {file}", name, path);
                return 0;
            }

            int count = 0;
            foreach (var item in result.Items)
            {
                string value = Clean(item.Value);
                if (value.Length == 0)
                    continue;

                bool mapped = normalizer.IsMapped(item.Namespace, item.Key);
                WriteRaw(name, item.Namespace, item.Key, value, item.SubResource?.Name, mapped, row);
                count++;

                foreach (var canon in normalizer.NormalizeAll(item.Namespace, item.Key, item.Value, provider.ProviderId, item.SubResource))
                    WriteCanonical(canon, name, item.SubResource?.Name, row);
            }
            return count;
        }

        private async Task<int> RunMetaCliAsync(string path, FileRow row)
        {
            MetadataCliResult? result;
            try
            {
                result = await metaCli.GetMetadataAsync(path);
            }
            catch (Exception ex)
            {
                // meta-cli only handles media; failing to open other files is the expected common case.
                logger.LogDebug("meta-cli skipped {file}: {msg}", path, ex.Message);
                return 0;
            }
            if (result is null)
                return 0;

            int count = 0;
            if (result.Metadata != null)
                foreach (var (key, value) in result.Metadata)
                    count += EmitMetaCli("metacli", key, value, null, row);

            if (result.Streams != null)
                foreach (var stream in result.Streams)
                    if (stream.Metadata != null)
                        foreach (var (key, value) in stream.Metadata)
                            count += EmitMetaCli("metacli.stream", key, value, $"stream {stream.Id}", row);

            return count;
        }

        private int EmitMetaCli(string ns, string key, object? rawValue, string? subResource, FileRow row)
        {
            string value = Clean(rawValue?.ToString());
            if (value.Length == 0)
                return 0;

            aliases.TryGetValue(key.Trim().ToLowerInvariant(), out var alias);
            bool ignored = alias is { Role: AliasRole.Ignore };
            bool mapped = alias is { Role: AliasRole.Default, AttributeDefinitionId: not null };

            // Explicitly-ignored keys aren't "unmapped to fix", so don't add them to the worklist.
            WriteRaw("meta-cli", ns, key, value, subResource, mapped, row, countUnmapped: !ignored);

            if (mapped && vocab.TryGetValue(alias!.AttributeDefinitionId!.Value, out var def))
                WriteCanonicalRow(def, value, "meta-cli", subResource, row);

            return 1;
        }

        private void WriteRaw(string provider, string ns, string key, string value, string? subResource, bool mapped, FileRow row, bool countUnmapped = true)
        {
            rawCsv.WriteRecord(new RawRow
            {
                File = row.File,
                Provider = provider,
                Namespace = ns,
                Key = key,
                Value = value,
                SubResource = subResource,
                Mapped = mapped,
            });
            rawCsv.NextRecord();

            totalRaw++;
            perProvider[provider] = perProvider.GetValueOrDefault(provider) + 1;

            if (!mapped && countUnmapped)
            {
                row.UnmappedItems++;
                totalUnmapped++;
                var compositeKey = (provider, ns.ToLowerInvariant(), key.Trim().ToLowerInvariant());
                if (!unmapped.TryGetValue(compositeKey, out var agg))
                    unmapped[compositeKey] = agg = new UnmappedAgg { Sample = value };
                agg.Files.Add(row.File);
                agg.Occurrences++;
            }
        }

        private void WriteCanonical(AttributeBase attribute, string sourceProvider, string? subResource, FileRow row)
        {
            if (vocab.TryGetValue(attribute.AttributeDefinitionId, out var def))
                WriteCanonicalRow(def, GetValue(attribute), sourceProvider, subResource, row);
        }

        private void WriteCanonicalRow(AttributeDefinition def, string value, string sourceProvider, string? subResource, FileRow row)
        {
            canonicalCsv.WriteRecord(new CanonicalRow
            {
                File = row.File,
                Group = def.Group ?? "",
                Attribute = def.Name,
                Value = Clean(value),
                Unit = def.Unit,
                SourceProvider = sourceProvider,
                SubResource = subResource,
            });
            canonicalCsv.NextRecord();
            totalCanonical++;
            row.CanonicalItems++;
        }

        public async Task FinishAsync()
        {
            await rawCsv.FlushAsync();
            await canonicalCsv.FlushAsync();

            var unmappedRows = unmapped
                .Select(kv => new UnmappedRow
                {
                    Provider = kv.Key.Provider,
                    Namespace = kv.Key.Namespace,
                    Key = kv.Key.Key,
                    Files = kv.Value.Files.Count,
                    Occurrences = kv.Value.Occurrences,
                    SampleValue = kv.Value.Sample,
                })
                .OrderByDescending(r => r.Files)
                .ThenByDescending(r => r.Occurrences)
                .ToList();

            await WriteCsvAsync(unmappedRows, Path.Combine(outputDir, "unmapped-keys.csv"));
            await WriteCsvAsync(fileRows, Path.Combine(outputDir, "file-summary.csv"));

            PrintSummary(unmappedRows);
        }

        private static async Task WriteCsvAsync<T>(IEnumerable<T> rows, string path)
        {
            await using var writer = new StreamWriter(path);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(rows);
        }

        private void PrintSummary(List<UnmappedRow> unmappedRows)
        {
            Console.WriteLine();
            Console.WriteLine("=== METADATA COLLECTION SUMMARY ===");
            Console.WriteLine($"Files processed : {fileRows.Count}  (OK={fileRows.Count(f => f.Status == "OK")}, Empty={fileRows.Count(f => f.Status == "Empty")}, Error={fileRows.Count(f => f.Status == "Error")})");
            Console.WriteLine($"Raw items       : {totalRaw}  (mapped {totalRaw - totalUnmapped}, unmapped {totalUnmapped})");
            Console.WriteLine($"Canonical items : {totalCanonical}");
            Console.WriteLine("By provider     : " + string.Join("  ", perProvider.OrderByDescending(p => p.Value).Select(p => $"{p.Key}={p.Value}")));
            Console.WriteLine();
            Console.WriteLine("Top unmapped keys (#files | provider | namespace:key | sample):");
            foreach (var u in unmappedRows.Take(25))
                Console.WriteLine($"  {u.Files,5}  {u.Provider,-9} {u.Namespace}:{Trunc(u.Key, 28),-28} | {Trunc(u.SampleValue, 45)}");
            Console.WriteLine();
            Console.WriteLine($"CSVs written to: {outputDir}");
        }

        private static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            string s = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return Trunc(s, MaxValueLength);
        }

        private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

        private static string GetValue(AttributeBase a) => a switch
        {
            TextAttribute t => t.Value,
            IntegerAttribute i => i.Value.ToString(CultureInfo.InvariantCulture),
            FloatAttribute f => f.Value.ToString(CultureInfo.InvariantCulture),
            DateAttribute d => d.Value.ToString("o", CultureInfo.InvariantCulture),
            BlobAttribute b => $"[{b.Value?.Length ?? 0} bytes]",
            _ => "",
        };

        public void Dispose()
        {
            rawCsv.Dispose();
            rawOut.Dispose();
            canonicalCsv.Dispose();
            canonicalOut.Dispose();
        }
    }
}
