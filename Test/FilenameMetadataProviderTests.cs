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
    }
}
