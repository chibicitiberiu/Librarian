using System.Text.RegularExpressions;
using Librarian.Model.MetadataAttributes;

namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// Derives metadata that lives only in a media file's <em>name</em> — chiefly TV episodes, whose
    /// container rarely carries series/season/episode tags. Recognises the common
    /// "<c>Series - SxxExx - Title</c>" convention (the season may be a year, e.g. <c>S1960E10</c>),
    /// strips trailing <c>[group]</c>/<c>(info)</c> tags, and promotes Series / Season / Episode /
    /// Title so the Video ▸ TV Shows facet works.
    /// </summary>
    public class FilenameMetadataProvider : IMetadataProvider
    {
        /// <summary>A TV episode parsed from a filename.</summary>
        public readonly record struct TvEpisode(string Series, int Season, int Episode, string? Title);

        private static readonly Guid providerId = new("f4c1e8a2-3b5d-4e7f-9a1c-2d6b8e0f4a37");

        private static readonly Regex TvEpisodePattern = new(
            @"^(?<series>.+?)\s*-\s*[Ss](?<season>\d{1,4})[Ee](?<episode>\d{1,3})\b(?:\s*-\s*(?<title>.+))?$",
            RegexOptions.Compiled);

        private static readonly Regex TrailingTag = new(@"\s*[\[\(][^\]\)]*[\]\)]\s*$", RegexOptions.Compiled);

        /// <summary>Parses "Series - SxxExx - Title" (season may be a year) from a filename stem,
        /// stripping a trailing [group]/(info) tag. Returns null when it isn't a TV episode.</summary>
        public static TvEpisode? ParseTvEpisode(string fileNameWithoutExtension)
        {
            string name = TrailingTag.Replace(fileNameWithoutExtension, string.Empty).Trim();

            var match = TvEpisodePattern.Match(name);
            if (!match.Success
                || !int.TryParse(match.Groups["season"].Value, out int season)
                || !int.TryParse(match.Groups["episode"].Value, out int episode))
                return null;

            string series = match.Groups["series"].Value.Trim();
            if (series.Length == 0)
                return null;

            string title = match.Groups["title"].Value.Trim();
            return new TvEpisode(series, season, episode, title.Length > 0 ? title : null);
        }

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
                Emit(Path.GetFileNameWithoutExtension(filePath), result);

            return Task.FromResult(result);
        }

        private void Emit(string name, MetadataCollection result)
        {
            if (ParseTvEpisode(name) is not { } ep)
                return;

            // The series name has no dedicated attribute; General.Collection ("the collection/series
            // this belongs to") is the natural home and avoids minting a new definition.
            result.Add(metadataFactory.Create(General.Collection, ep.Series, ProviderId, editable: true));
            result.Add(metadataFactory.Create(Media.SeasonNumber, ep.Season, ProviderId, editable: true));
            result.Add(metadataFactory.Create(Media.EpisodeNumber, ep.Episode, ProviderId, editable: true));
            if (ep.Title is not null)
                result.Add(metadataFactory.Create(General.Title, ep.Title, ProviderId, editable: true));
        }

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
