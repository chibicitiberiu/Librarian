using Librarian.Library;
using Xunit;

namespace Test
{
    public class MediaTypeTests
    {
        [Theory]
        // A Windows .exe: file-service says the generic octet-stream, Tika says x-msdownload.
        // The specific signal must win, regardless of which source it came from.
        [InlineData("application/octet-stream", "application/x-msdownload", MediaClass.Software, "Windows executable")]
        // The reverse: file-service has the specific type, Tika went generic.
        [InlineData("image/jpeg", "application/octet-stream", MediaClass.Image, "JPEG image")]
        [InlineData("audio/x-flac", null, MediaClass.Audio, "FLAC audio")]
        [InlineData("audio/mpeg", null, MediaClass.Audio, "MP3 audio")]
        [InlineData("video/mp4", null, MediaClass.Video, "MP4 video")]
        [InlineData("image/png", null, MediaClass.Image, "PNG image")]
        [InlineData("application/pdf", null, MediaClass.Document, "PDF document")]
        [InlineData("application/msword", null, MediaClass.Document, "Word document")]
        [InlineData("application/x-mobipocket-ebook", null, MediaClass.Document, "E-book")]
        [InlineData("application/epub+zip", null, MediaClass.Document, "E-book")]
        [InlineData("application/zip", null, MediaClass.Archive, "Archive")]
        [InlineData("inode/directory", null, MediaClass.Folder, "Folder")]
        public void Resolves_expected_class_and_label(string? fileMime, string? tikaMime, MediaClass cls, string label)
        {
            var r = MediaType.Resolve(fileMime, tikaMime);
            Assert.Equal(cls, r.Class);
            Assert.Equal(label, r.Label);
        }

        [Theory]
        [InlineData("application/octet-stream", "application/octet-stream")]
        [InlineData(null, null)]
        [InlineData("", "   ")]
        public void Unclassifiable_is_unknown(string? fileMime, string? tikaMime)
        {
            var r = MediaType.Resolve(fileMime, tikaMime);
            Assert.Equal(MediaClass.Other, r.Class);
            Assert.Equal("Unknown", r.Label);
        }

        [Fact]
        public void Strips_mime_parameters()
        {
            var r = MediaType.Resolve("text/plain; charset=utf-8", null);
            Assert.Equal(MediaClass.Document, r.Class);
            Assert.Equal("Text document", r.Label);
        }

        [Fact]
        public void Prefers_tika_when_both_specific()
        {
            // Both non-generic but disagree → Tika (the richer parser) wins.
            var r = MediaType.Resolve("application/zip", "application/epub+zip");
            Assert.Equal("E-book", r.Label);
        }

        [Fact]
        public void Falls_back_to_readable_label_for_unknown_subtype()
        {
            var r = MediaType.Resolve("application/x-doom", null);
            Assert.Equal(MediaClass.Other, r.Class);
            Assert.Equal("Doom", r.Label);
        }
    }
}
