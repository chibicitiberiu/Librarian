using Librarian.Library;
using Xunit;

namespace Test
{
    public class SidecarsTests
    {
        [Theory]
        [InlineData("metadata.opf", SidecarKind.Opf)]
        [InlineData("Book.OPF", SidecarKind.Opf)]
        [InlineData("08 - Love of My Life.lrc", SidecarKind.Lrc)]
        [InlineData("FILE_ID.NFO", SidecarKind.Nfo)]
        [InlineData("release.nfo", SidecarKind.Nfo)]
        [InlineData("cover.jpg", SidecarKind.CompanionArt)]
        [InlineData("folder.png", SidecarKind.CompanionArt)]
        [InlineData("backdrop.jpeg", SidecarKind.CompanionArt)]
        [InlineData("logo.png", SidecarKind.CompanionArt)]
        [InlineData("Cover.JPG", SidecarKind.CompanionArt)]
        // Content: real primaries and lookalikes that must NOT be swallowed.
        [InlineData("01 - Track.flac", SidecarKind.Content)]
        [InlineData("Computer Networks - Tanenbaum.pdf", SidecarKind.Content)]
        [InlineData("cover.txt", SidecarKind.Content)]     // art stem but not an image
        [InlineData("vacation.jpg", SidecarKind.Content)]  // image but not an art stem
        [InlineData("OUTPOST2.EXE", SidecarKind.Content)]
        // Resource files (M3b) — never standalone library content.
        [InlineData("CEP1.DLL", SidecarKind.CompanionResource)]
        [InlineData("ATTACK.ANI", SidecarKind.CompanionResource)]
        [InlineData("game.vol", SidecarKind.CompanionResource)]
        [InlineData("setup.inf", SidecarKind.CompanionResource)]
        [InlineData("config.ini", SidecarKind.CompanionResource)]
        [InlineData("libfoo.so", SidecarKind.CompanionResource)]
        // Subtitles fold into their video — never standalone items (and so they don't block promotion).
        [InlineData("Madagascar (2005)-rum.srt", SidecarKind.CompanionResource)]
        [InlineData("movie.sub", SidecarKind.CompanionResource)]
        [InlineData("movie.idx", SidecarKind.CompanionResource)]
        [InlineData("movie.ass", SidecarKind.CompanionResource)]
        [InlineData("movie.vtt", SidecarKind.CompanionResource)]
        public void Classifies_by_name(string fileName, SidecarKind expected)
        {
            Assert.Equal(expected, Sidecars.Classify(fileName));
        }

        [Theory]
        [InlineData("movie.mkv", true, true)]    // video → primary media
        [InlineData("clip.AVI", true, true)]
        [InlineData("song.flac", false, true)]   // audio → primary media (not video)
        [InlineData("game.exe", false, true)]    // executable → primary media
        [InlineData("cover.jpg", false, false)]
        [InlineData("subtitle.srt", false, false)]
        [InlineData("notes.txt", false, false)]
        public void Video_and_primary_media_predicates(string name, bool video, bool primaryMedia)
        {
            Assert.Equal(video, Sidecars.IsVideo(name));
            Assert.Equal(primaryMedia, Sidecars.IsPrimaryMedia(name));
        }

        // The whole point of detecting content MIME: it overrides a lying extension. A video with a .txt
        // name is still a video; a real text file with a video extension is not.
        [Theory]
        [InlineData("movie.txt", "video/mp4", true)]
        [InlineData("data.bin", "audio/x-flac", true)]
        [InlineData("clip.mkv", "text/plain", false)]   // MIME says text → not primary media
        public void Content_mime_overrides_extension(string name, string mime, bool primaryMedia)
            => Assert.Equal(primaryMedia, Sidecars.IsPrimaryMedia(name, mime));

        [Fact]
        public void Image_mime_with_art_stem_is_companion_art_even_with_wrong_extension()
            => Assert.Equal(SidecarKind.CompanionArt, Sidecars.Classify("cover.dat", "image/png"));

        [Theory]
        [InlineData("OP2_ART.BMP", true, false, false)]
        [InlineData("track.flac", false, true, false)]
        [InlineData("OUTPOST2.EXE", false, false, true)]
        [InlineData("readme.txt", false, false, false)]
        public void Media_class_predicates(string name, bool image, bool audio, bool exe)
        {
            Assert.Equal(image, Sidecars.IsImage(name));
            Assert.Equal(audio, Sidecars.IsAudio(name));
            Assert.Equal(exe, Sidecars.IsExecutable(name));
        }

        [Theory]
        [InlineData("08 - Love of My Life.LRC", "08 - love of my life")]
        [InlineData("Track.flac", "track")]
        public void Stem_is_lowercased_without_extension(string fileName, string expected)
        {
            Assert.Equal(expected, Sidecars.Stem(fileName));
        }

        [Theory]
        [InlineData("setup.exe", true)]
        [InlineData("UNINS000.EXE", true)]
        [InlineData("RAR.EXE", true)]
        [InlineData("vcredist.exe", true)]
        [InlineData("OUTPOST2.EXE", false)]
        [InlineData("game.exe", false)]
        [InlineData("readme.txt", false)]   // not even an executable
        public void Identifies_auxiliary_executables(string fileName, bool aux)
        {
            Assert.Equal(aux, Sidecars.IsAuxiliaryExecutable(fileName));
        }

        [Theory]
        [InlineData("Outpost 2", "OUTPOST2.EXE")]      // folder name lines up with the app exe
        [InlineData("Tom & Jerry!", "tomjerry")]
        public void Match_key_normalizes_for_comparison(string a, string b)
        {
            Assert.Equal(Sidecars.MatchKey(a), Sidecars.MatchKey(System.IO.Path.GetFileNameWithoutExtension(b)));
        }
    }
}
