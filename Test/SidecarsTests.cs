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
        public void Classifies_by_name(string fileName, SidecarKind expected)
        {
            Assert.Equal(expected, Sidecars.Classify(fileName));
        }

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
