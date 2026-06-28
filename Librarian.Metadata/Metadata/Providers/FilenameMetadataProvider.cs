using System.Text.RegularExpressions;
using Librarian.Model.MetadataAttributes;

namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// Derives metadata that lives only in a media file's <em>name and folder</em> — chiefly TV
    /// episodes, whose container rarely carries series/season/episode tags. The series name is usually
    /// the folder, not the filename (e.g. <c>Looney Tunes/Season 1947/S1947E07 - Tweetie Pie.avi</c>),
    /// so we read both. Recognises <c>SxxExx</c> (season may be a year like <c>S1947</c>, episode up to
    /// six digits as ytdl-sub encodes dates, optional <c>-NN</c> multi-episode range) and <c>NxNN</c>,
    /// with or without a series prefix, and falls back to the show folder above a <c>Season N</c> folder.
    /// </summary>
    public class FilenameMetadataProvider : IMetadataProvider
    {
        /// <summary>A TV episode parsed from a filename (+ folder context). <see cref="Series"/> may be
        /// empty when neither the name nor the folder yields one.</summary>
        public readonly record struct TvEpisode(string Series, int Season, int Episode, string? Title);

        private static readonly Guid providerId = new("f4c1e8a2-3b5d-4e7f-9a1c-2d6b8e0f4a37");

        // "[Ss]eason 1947", "S 02", "Specials", and a few common localisations — a folder that groups a
        // season rather than the show itself, so the show name is the folder *above* it.
        private static readonly Regex SeasonFolder = new(
            @"^(?:season|series|saison|temporada|staffel|sezon(?:ul)?|s|specials?)[ ._-]*\d*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "SxxExx" — season 1-4 digits (years are used as seasons), episode 1-6 digits (ytdl-sub encodes
        // a date), optional "-NN" range. A flexible separator covers "S01E02", "s2016.e081901", "S01 E02".
        private static readonly Regex SeasonEpisode = new(
            @"^(?<series>.*?)\b[Ss](?<season>\d{1,4})[ ._]?[Ee](?<episode>\d{1,6})(?:-\d{1,6})?\b(?<title>.*)$",
            RegexOptions.Compiled);

        // "1x02" style.
        private static readonly Regex NxNN = new(
            @"^(?<series>.*?)\b(?<season>\d{1,2})x(?<episode>\d{1,4})\b(?<title>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex TrailingTag = new(@"\s*[\[\(][^\]\)]*[\]\)]\s*$", RegexOptions.Compiled);
        private static readonly Regex TrailingYear = new(@"\s*\((?:19|20)\d{2}\)\s*$", RegexOptions.Compiled);
        private static readonly Regex EdgeSeparators = new(@"^[\s._\-–—]+|[\s._\-–—]+$", RegexOptions.Compiled);

        /// <summary>Parses an episode from a filename stem, deriving the series from the surrounding
        /// folders when the name has no series prefix. Returns null when it isn't a recognisable episode.</summary>
        /// <param name="fileNameWithoutExtension">The filename without path or extension.</param>
        /// <param name="immediateFolder">Name of the directory the file sits in (may be a "Season N").</param>
        /// <param name="parentFolder">Name of the directory above that (the show, when the file is in a season folder).</param>
        public static TvEpisode? ParseTvEpisode(string fileNameWithoutExtension, string? immediateFolder = null, string? parentFolder = null)
        {
            string name = TrailingTag.Replace(fileNameWithoutExtension, string.Empty).Trim();

            var match = SeasonEpisode.Match(name);
            if (!match.Success)
                match = NxNN.Match(name);
            if (!match.Success
                || !int.TryParse(match.Groups["season"].Value, out int season)
                || !int.TryParse(match.Groups["episode"].Value, out int episode))
                return null;

            string series = Clean(match.Groups["series"].Value);
            if (series.Length == 0)
                series = SeriesFromFolders(immediateFolder, parentFolder);

            string title = Clean(match.Groups["title"].Value);
            return new TvEpisode(series, season, episode, title.Length > 0 ? title : null);
        }

        /// <summary>The show name from the folder structure: the show folder is the one above a
        /// "Season N" folder, otherwise the immediate folder itself. A trailing "(year)" is dropped.</summary>
        private static string SeriesFromFolders(string? immediateFolder, string? parentFolder)
        {
            string? show = !string.IsNullOrWhiteSpace(immediateFolder) && SeasonFolder.IsMatch(immediateFolder.Trim())
                ? parentFolder
                : immediateFolder;
            return show is null ? string.Empty : TrailingYear.Replace(show.Trim(), string.Empty).Trim();
        }

        private static string Clean(string value) => EdgeSeparators.Replace(value.Trim(), string.Empty).Trim();

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".mpg", ".mpeg", ".webm", ".flv", ".ts",
        };

        private readonly MetadataFactory metadataFactory;

        public Guid ProviderId => providerId;
        public string DisplayName => "Filename parser";

        public FilenameMetadataProvider(MetadataFactory metadataFactory)
        {
            this.metadataFactory = metadataFactory;
        }

        public Task<MetadataCollection> GetMetadataAsync(string filePath)
        {
            var result = new MetadataCollection();

            if (File.Exists(filePath) && VideoExtensions.Contains(Path.GetExtension(filePath)))
            {
                string? immediate = Path.GetFileName(Path.GetDirectoryName(filePath));
                string? parent = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));
                Emit(Path.GetFileNameWithoutExtension(filePath), immediate, parent, result);
            }

            return Task.FromResult(result);
        }

        private void Emit(string name, string? immediateFolder, string? parentFolder, MetadataCollection result)
        {
            if (ParseTvEpisode(name, immediateFolder, parentFolder) is not { } ep)
                return;

            result.Add(metadataFactory.Create(Media.SeasonNumber, ep.Season, ProviderId, editable: true));
            result.Add(metadataFactory.Create(Media.EpisodeNumber, ep.Episode, ProviderId, editable: true));
            // The series name has no dedicated attribute; General.Collection ("the collection/series
            // this belongs to") is the natural home and avoids minting a new definition.
            if (ep.Series.Length > 0)
                result.Add(metadataFactory.Create(General.Collection, ep.Series, ProviderId, editable: true));
            if (ep.Title is not null)
                result.Add(metadataFactory.Create(General.Title, ep.Title, ProviderId, editable: true));
        }

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
