using CsvHelper;
using Librarian.Metadata.Providers.MetadataCli;
using Librarian.Util;
using Microsoft.Extensions.Configuration;
using MimeMapping;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using System.Diagnostics.SymbolStore;
using System.Globalization;

internal class Program
{
    private static readonly Dictionary<string, string?> Configuration = new()
    {
        ["MetadataCliPath"] = "/home/tibi/.vs/meta-cli/out/build/linux-debug/meta-cli"
    };

    private static readonly string[] RootDirectories = {
        @"Y:\Downloads",
        @"D:\Copii",
        @"D:\Downloads",
        @"D:\Retro Game Collection"
    };

    private static CsvWriter FileInfoWriter;
    private static CsvWriter MetadataWriter;
    private static MetadataCliService CliService;

    private static string ToWslPath(string path)
    {
        if (path.Contains(":\\"))
        {
            return string.Format("/mnt/{0}{1}",
                char.ToLowerInvariant(path[0]),
                path[2..].Replace("\\", "/"));
        }
        return path.Replace("\\", "/");
    }

    private static bool IsRunningInWsl()
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            string version = File.ReadAllText("/proc/version");
            return version.Contains("microsoft", StringComparison.InvariantCultureIgnoreCase);
        }
        return false;
    }

    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(Configuration)
            .Build();

        CliService = new MetadataCliService(config);

        // Open output writers
        using var fileInfoOut = new StreamWriter("/mnt/c/Temp/file-info.csv");
        using var metadataOut = new StreamWriter("/mnt/c/Temp/all-metadata.csv");
        FileInfoWriter = new CsvWriter(fileInfoOut, CultureInfo.InvariantCulture);
        FileInfoWriter.WriteHeader(typeof(FileInfo));
        FileInfoWriter.NextRecord();
        MetadataWriter = new CsvWriter(metadataOut, CultureInfo.InvariantCulture);
        MetadataWriter.WriteHeader(typeof(FileMetadata));
        MetadataWriter.NextRecord();

        // Process directories
        IEnumerable<string> rootDirs = RootDirectories;
        if (IsRunningInWsl())
            rootDirs = rootDirs.Select(ToWslPath);
        rootDirs.Select(x => new DirectoryInfo(x)).ForEach(ProcessDirectory);
    }

    private static bool IsFileRelevant(string fileName, string mimeType)
    {
        fileName = fileName.ToLowerInvariant();
        mimeType = mimeType.ToLowerInvariant();

        bool interesting = mimeType.StartsWith("audio");
        interesting |= mimeType.StartsWith("video");
        return interesting;
    }

    private static void ProcessDirectory(DirectoryInfo directory)
    {
        directory.EnumerateDirectories().ForEach(ProcessDirectory);
        Console.WriteLine($"Processing {directory}");

        foreach (var file in directory.EnumerateFiles())
        {
            string mimeType = MimeUtility.GetMimeMapping(file.FullName);
            bool relevant = IsFileRelevant(file.Name, mimeType);
            bool couldCollect = true;

            // record metadata
            JObject? metadata = null;
            try
            {
                //metadata = CliService.GetMetadataAsync(file.FullName).WaitAndUnwrapException();
            }
            catch (Exception ex)
            {
                couldCollect = false;
            }

            // record file info
            FileInfoWriter.WriteRecord(new FileInfo(
                file.FullName,
                file.Name,
                file.Extension,
                mimeType,
                relevant,
                couldCollect
            ));
            FileInfoWriter.NextRecord();

            foreach (var (key, value) in WalkMetadata(metadata))
            {
                MetadataWriter.WriteRecord(new FileMetadata(
                    file.FullName,
                    file.Extension,
                    mimeType,
                    key,
                    value
                ));
                MetadataWriter.NextRecord();
            }
        }
    }

    static IEnumerable<(string, object?)> WalkMetadata(JToken? node)
    {
        if (node == null)
            yield break;

        switch (node.Type)
        {
            case JTokenType.Object:
                foreach (JProperty child in node.Children<JProperty>())
                {
                    foreach (var result in WalkMetadata(child.Value))
                        yield return result;
                }
                break;

            case JTokenType.Array:
                foreach (JToken child in node.Children())
                {
                    foreach (var result in WalkMetadata(child))
                        yield return result;
                }
                break;

            default:
                yield return (node.Path, node.Value<object>());
                break;
        }
    }
}
