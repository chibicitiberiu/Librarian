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
    /// Standalone tool that runs the real metadata collectors (Tika + ExifTool raw providers, meta-cli)
    /// over a set of files and dumps what they extract — including, per (namespace, key), whether the
    /// pipeline currently has a rule/alias to promote it. Database-free: the raw providers and the
    /// normalizer are pure, and the vocabulary/aliases are read from the embedded CSVs. The point is to
    /// see "what does our extraction look like" across a large, real library and to surface the
    /// unmapped keys worth adding rules for.
    /// </summary>
    internal static class Program
    {
        // The hidden user-edit sidecar should never be treated as a content file.
        private const string Sidecar = ".librarian.meta";

        private static async Task<int> Main(string[] args)
        {
            Options? options = Options.Parse(args);
            if (options is null)
            {
                Options.PrintUsage();
                return 1;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TikaUrl"] = options.TikaUrl,
                    ["MetadataCliPath"] = options.MetadataCliPath,
                    ["ExifToolPath"] = options.ExifToolPath,
                })
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
                builder.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Warning);
            });
            var logger = loggerFactory.CreateLogger("collector");

            var files = EnumerateFiles(options, logger).ToList();
            if (files.Count == 0)
            {
                logger.LogError("No files matched the given paths/filters.");
                return 1;
            }

            Console.WriteLine($"Collecting metadata from {files.Count} file(s) -> {options.Output}");
            Console.WriteLine($"  Tika={options.TikaUrl}  meta-cli={options.MetadataCliPath}  exiftool={options.ExifToolPath}");

            using var collector = new Collector(configuration, loggerFactory, options.Output);

            int processed = 0;
            foreach (var file in files)
            {
                processed++;
                if (processed % 50 == 0 || options.Verbose)
                    Console.WriteLine($"  [{processed}/{files.Count}] {file}");
                await collector.ProcessFileAsync(file);
            }

            await collector.FinishAsync();
            return 0;
        }

        private static IEnumerable<string> EnumerateFiles(Options options, ILogger logger)
        {
            var filter = options.Filter.Select(f => f.StartsWith('.') ? f : "." + f).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int yielded = 0;

            foreach (var path in options.Paths)
            {
                IEnumerable<string> candidates;
                if (File.Exists(path))
                    candidates = new[] { path };
                else if (Directory.Exists(path))
                    candidates = SafeEnumerate(path, options.Recursive, logger);
                else
                {
                    logger.LogWarning("Path not found: {path}", path);
                    continue;
                }

                foreach (var file in candidates)
                {
                    if (filter.Count > 0 && !filter.Contains(Path.GetExtension(file)))
                        continue;
                    if (Path.GetFileName(file).Equals(Sidecar, StringComparison.OrdinalIgnoreCase))
                        continue;
                    yield return file;
                    if (options.MaxFiles > 0 && ++yielded >= options.MaxFiles)
                        yield break;
                }
            }
        }

        private static IEnumerable<string> SafeEnumerate(string root, bool recursive, ILogger logger)
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true };
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", opts); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not enumerate {root}", root); yield break; }
            foreach (var f in files)
                yield return f;
        }
    }
}
