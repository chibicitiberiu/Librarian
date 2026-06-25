namespace MetadataCollector
{
    /// <summary>Command-line options (hand-parsed, no external dependency).</summary>
    internal sealed class Options
    {
        public List<string> Paths { get; } = new();
        public string Output { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "metadata-results");
        public bool Recursive { get; set; }
        public bool Verbose { get; set; }
        public List<string> Filter { get; } = new();
        public int MaxFiles { get; set; }
        public string TikaUrl { get; set; } = "http://localhost:9998";
        public string MetadataCliPath { get; set; } = "";
        public string ExifToolPath { get; set; } = "exiftool";

        public static Options? Parse(string[] args)
        {
            if (args.Length == 0)
                return null;

            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h" or "--help": return null;
                    case "-r" or "--recursive": o.Recursive = true; break;
                    case "-v" or "--verbose": o.Verbose = true; break;
                    case "-o" or "--output": o.Output = NextArg(args, ref i) ?? o.Output; break;
                    case "--max-files": if (int.TryParse(NextArg(args, ref i), out int m)) o.MaxFiles = m; break;
                    case "--tika-url": o.TikaUrl = NextArg(args, ref i) ?? o.TikaUrl; break;
                    case "--metadata-cli-path": o.MetadataCliPath = NextArg(args, ref i) ?? o.MetadataCliPath; break;
                    case "--exiftool-path": o.ExifToolPath = NextArg(args, ref i) ?? o.ExifToolPath; break;
                    case "-p" or "--path": CollectList(args, ref i, o.Paths); break;
                    case "--filter": CollectList(args, ref i, o.Filter); break;
                    default:
                        if (args[i].StartsWith('-'))
                        {
                            Console.Error.WriteLine($"Unknown option: {args[i]}");
                            return null;
                        }
                        o.Paths.Add(args[i]); // bare positional = path
                        break;
                }
            }

            return o.Paths.Count > 0 ? o : null;
        }

        private static string? NextArg(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : null;

        // Consume following non-flag tokens as list values (supports "-p a b c" and "--filter .mp3 .flac").
        private static void CollectList(string[] args, ref int i, List<string> target)
        {
            while (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                target.Add(args[++i]);
        }

        public static void PrintUsage()
        {
            Console.WriteLine(
                """
                MetadataCollector — runs the real collectors (Tika + ExifTool + meta-cli) over files and
                dumps what they extract, flagging which keys the pipeline can currently map.

                Usage:
                  MetadataCollector <path...> [options]
                  MetadataCollector -p <path...> [options]

                Options:
                  -p, --path <paths...>        Files or directories to scan (also accepted positionally)
                  -r, --recursive              Recurse into subdirectories
                      --filter <ext...>        Only these extensions (e.g. --filter .mp3 .flac .mkv)
                      --max-files <n>          Stop after n files
                  -o, --output <dir>           Output directory (default ./metadata-results)
                      --tika-url <url>         Tika server URL (default http://localhost:9998)
                      --metadata-cli-path <p>  meta-cli binary (default: disabled)
                      --exiftool-path <p>      exiftool binary (default: exiftool on PATH)
                  -v, --verbose                Verbose logging
                  -h, --help                   This help

                Outputs (CSV) in the output dir:
                  raw-metadata.csv        every (provider, namespace, key, value) + Mapped flag
                  canonical-metadata.csv  what the pipeline would store after promotion/aliasing
                  unmapped-keys.csv       unmapped (provider, namespace, key) ranked by #files — the rules worklist
                  file-summary.csv        per-file counts and status
                """);
        }
    }
}
