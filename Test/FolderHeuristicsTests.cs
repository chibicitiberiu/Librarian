using Librarian.Library;
using Xunit;

namespace Test
{
    /// <summary>
    /// Regression tests for the folder→item promotion heuristic, seeded from real library cases. When the
    /// algorithm is tweaked as more data is ingested, these pin the decisions that must not regress.
    /// </summary>
    public class FolderHeuristicsTests
    {
        private static bool Promote(string folder, string primary, params string[] siblings)
        {
            // The primary is always one of the folder's files.
            var files = new System.Collections.Generic.List<string>(siblings) { primary };
            return FolderHeuristics.FolderIsDedicatedToItem(folder, primary, files);
        }

        // ---- Promote: the folder is dedicated to its single item -------------------------------------

        [Theory]
        // Clean folder + clean file name.
        [InlineData("Madagascar (2005)", "Madagascar.avi")]
        // Release-scene filename full of noise tokens — the folder's clean title still matches.
        [InlineData("Frozen (2013)", "Frozen.2013.1080p.BluRay.DD5.1.x264.RoDubbed-playHD.mkv")]
        [InlineData("WALL-E (2008)", "WALL-E.2008.1080p.UHD.BluRay.DD-EX5.1.HDR.x265.RoDubbed-playHD.mkv")]
        // Multi-word title fully present in the file.
        [InlineData("Inception 2 (2029)", "Inception 2.mkv")]
        [InlineData("The Action Lab", "The Action Lab - Episode 1.mp4")]
        // Generic filename → trust the descriptive folder.
        [InlineData("Inception 2 (2029)", "movie.mkv")]
        [InlineData("Some Movie (2010)", "VTS_01_1.vob")]
        public void Dedicated_folders_promote(string folder, string primary)
            => Assert.True(Promote(folder, primary));

        [Fact]
        public void Disc_rip_markers_promote_even_with_unrelated_filename()
            => Assert.True(Promote("Inception (2010)", "movie.mkv", "VIDEO_TS.IFO", "VTS_01_1.VOB"));

        // ---- Don't promote: the folder is a container (becomes a one-item collection) ----------------

        [Theory]
        // Section folders: their name is not in the content file.
        [InlineData("Movies", "Inception.mkv")]
        [InlineData("Filme", "Inception.mkv")]          // language-agnostic: no shared token, no denylist
        [InlineData("Action", "Inception.mkv")]
        [InlineData("Unsorted Movies", "Zootopia (2016).mkv")]
        // Cross-language: English folder vs Romanian file share only the brand "shrek" — ambiguous, so the
        // safe default is to keep it a collection.
        [InlineData("Shrek forever after (dublat romana)", "Shrek pentru totdeauna Blu-RayRip.avi")]
        public void Container_folders_do_not_promote(string folder, string primary)
            => Assert.False(Promote(folder, primary));

        // ---- Token extraction ------------------------------------------------------------------------

        [Fact]
        public void TitleTokens_drops_year_and_release_noise()
        {
            var tokens = FolderHeuristics.TitleTokens("Frozen.2013.1080p.BluRay.x264-playHD");
            Assert.Contains("frozen", tokens);
            Assert.DoesNotContain("2013", tokens);     // year
            Assert.DoesNotContain("1080p", tokens);    // noise
            Assert.DoesNotContain("bluray", tokens);   // noise
            Assert.DoesNotContain("x264", tokens);     // noise
        }
    }
}
