using Librarian.Metadata.Providers;
using Xunit;

namespace Test
{
    public class FilenameMetadataProviderTests
    {
        [Fact]
        public void Parses_series_season_episode_title_and_strips_tag()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("Tom and Jerry - S1960E10 - Tall in the Trap [Tuddorr]");
            Assert.NotNull(ep);
            Assert.Equal("Tom and Jerry", ep!.Value.Series);
            Assert.Equal(1960, ep.Value.Season);     // a year used as the season
            Assert.Equal(10, ep.Value.Episode);
            Assert.Equal("Tall in the Trap", ep.Value.Title);
        }

        [Fact]
        public void Parses_episode_without_title()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("Show - S02E05");
            Assert.NotNull(ep);
            Assert.Equal("Show", ep!.Value.Series);
            Assert.Equal(2, ep.Value.Season);
            Assert.Equal(5, ep.Value.Episode);
            Assert.Null(ep.Value.Title);
        }

        [Theory]
        [InlineData("01 - Some Song")]                 // music track, not SxxExx
        [InlineData("Computer Networks - Tanenbaum")]   // a book
        [InlineData("random file")]
        public void Returns_null_for_non_episodes(string name)
        {
            Assert.Null(FilenameMetadataProvider.ParseTvEpisode(name));
        }

        // The series is usually the folder, not the filename — the provider reads both.
        [Theory]
        // name, immediateFolder, parentFolder, series, season, episode, title
        [InlineData("S01E11 - O minciună albă ca zăpada", "Kit si Keit", "Shows", "Kit si Keit", 1, 11, "O minciună albă ca zăpada")]
        [InlineData("s2016.e081901 - The Alphabet for Children", "Season 2016", "Kids Learning Tube - Wild", "Kids Learning Tube - Wild", 2016, 81901, "The Alphabet for Children")]
        [InlineData("S1947E07 - Tweetie Pie", "Season 1947", "Looney Tunes", "Looney Tunes", 1947, 7, "Tweetie Pie")]
        [InlineData("S01E53-54 - It's Time to Go", "Season 1", "Daniel Tiger's Neighborhood", "Daniel Tiger's Neighborhood", 1, 53, "It's Time to Go")]
        [InlineData("S01E01 - Mechanimals", "Xploration Earth 2050", "Educational Series", "Xploration Earth 2050", 1, 1, "Mechanimals")]
        public void Parses_episode_with_series_from_folder(string name, string folder, string parent, string series, int season, int episode, string title)
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode(name, folder, parent);
            Assert.NotNull(ep);
            Assert.Equal(series, ep!.Value.Series);
            Assert.Equal(season, ep.Value.Season);
            Assert.Equal(episode, ep.Value.Episode);
            Assert.Equal(title, ep.Value.Title);
        }

        [Fact]
        public void Series_prefix_in_filename_wins_over_folder()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("Some Show - S02E05 - The One", "Wrong Folder", "x");
            Assert.Equal("Some Show", ep!.Value.Series);
        }

        [Fact]
        public void Strips_trailing_year_from_folder_series()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("S01E02 - Deserts", "Season 1", "Prehistoric Planet (2022)");
            Assert.Equal("Prehistoric Planet", ep!.Value.Series);
        }

        [Fact]
        public void Parses_NxNN_form()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("Show - 1x05 - Pilot", "Show", "x");
            Assert.Equal(1, ep!.Value.Season);
            Assert.Equal(5, ep.Value.Episode);
            Assert.Equal("Pilot", ep.Value.Title);
        }

        [Fact]
        public void Normalizes_scene_style_dotted_series()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("Star.Wars.The.Clone.Wars.s01e06");
            Assert.Equal("Star Wars The Clone Wars", ep!.Value.Series);
            Assert.Equal(1, ep.Value.Season);
            Assert.Equal(6, ep.Value.Episode);
        }

        [Fact]
        public void Keeps_spaced_series_dots_untouched()
        {
            // Already has spaces -> dots are meaningful, leave them.
            var ep = FilenameMetadataProvider.ParseTvEpisode("Dr. Who - S01E01 - Rose");
            Assert.Equal("Dr. Who", ep!.Value.Series);
        }

        [Fact]
        public void Episode_without_title_uses_folder_series()
        {
            var ep = FilenameMetadataProvider.ParseTvEpisode("S03E09", "Season 03", "Xploration Outer Space");
            Assert.Equal("Xploration Outer Space", ep!.Value.Series);
            Assert.Equal(3, ep.Value.Season);
            Assert.Equal(9, ep.Value.Episode);
            Assert.Null(ep.Value.Title);
        }
    }
}
