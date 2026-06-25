using Librarian.Metadata;
using Xunit;

namespace Test
{
    /// <summary>
    /// Archive detection drives whether a file's entries are exploded into virtual files
    /// (collection_plan.md §7.1). It must match real archives (including comic-book and tarball compound
    /// extensions) and must NOT match non-archive containers like PDFs or mail.
    /// </summary>
    public class ArchiveTypesTests
    {
        [Theory]
        [InlineData("music/album.zip")]
        [InlineData("comics/issue.cbz")]
        [InlineData("backup.tar")]
        [InlineData("backup.tar.gz")]
        [InlineData("backup.tar.bz2")]
        [InlineData("backup.tar.xz")]
        [InlineData("game.7z")]
        [InlineData("scans.cbr")]
        [InlineData("ARCHIVE.ZIP")]
        public void Detects_archives(string path) => Assert.True(ArchiveTypes.IsArchive(path));

        [Theory]
        [InlineData("doc.pdf")]
        [InlineData("mail.eml")]
        [InlineData("song.flac")]
        [InlineData("movie.mkv")]
        [InlineData("photo.jpg")]
        [InlineData("notes.txt")]
        [InlineData("noextension")]
        [InlineData("")]
        // Java/zip-based executable bundles are a single unit (like an .exe), not an archive to explode.
        [InlineData("app.jar")]
        [InlineData("webapp.war")]
        [InlineData("enterprise.ear")]
        public void Rejects_non_archives(string path) => Assert.False(ArchiveTypes.IsArchive(path));

        [Fact]
        public void Bare_gz_is_not_treated_as_archive()
        {
            // A lone .gz (single compressed stream, not a tarball) is not an entry container.
            Assert.False(ArchiveTypes.IsArchive("logs/app.log.gz"));
        }
    }
}
