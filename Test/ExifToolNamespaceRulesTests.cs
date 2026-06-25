using System;
using System.Linq;
using Librarian.Metadata.Normalization;
using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Xunit;

namespace Test
{
    /// <summary>Rules for the ExifTool namespaces (vorbis:, id3:, exe:) and the cataloguing/codec extras
    /// that the collector run surfaced as unmapped.</summary>
    public class ExifToolNamespaceRulesTests
    {
        private static readonly Guid ProviderId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private readonly MetadataNormalizer normalizer = new();

        private int Def(string ns, string key, string value) => normalizer.Normalize(ns, key, value, ProviderId)!.AttributeDefinitionId;

        [Theory]
        [InlineData("title", "Da Coconut Nut")]
        [InlineData("artist", "Smokey Mountain")]
        [InlineData("album", "Paraiso")]
        [InlineData("albumartist", "Smokey Mountain")]
        [InlineData("albumartistsort", "Brickman, Jim")]
        [InlineData("label", "Windham Hill Records")]
        public void Vorbis_text_tags_map(string key, string value)
            => Assert.Equal(value, ((TextAttribute)normalizer.Normalize("vorbis", key, value, ProviderId)!).Value);

        [Fact]
        public void Vorbis_targets_match_tika_equivalents()
        {
            Assert.Equal(Audio.Artist, Def("vorbis", "artist", "x"));
            Assert.Equal(Audio.AlbumArtist, Def("vorbis", "albumartist", "x"));
            Assert.Equal(Audio.AlbumArtistSort, Def("vorbis", "albumartistsort", "x"));
            Assert.Equal(Audio.Track, Def("vorbis", "tracknumber", "3"));
            Assert.Equal(Media.MusicBrainzTrackID, Def("vorbis", "musicbrainztrackid", "x"));
            Assert.Equal(Media.MediaFormat, Def("vorbis", "media", "HDCD"));
            Assert.Equal(General.ReleaseStatus, Def("vorbis", "releasestatus", "official"));
        }

        [Fact]
        public void Vorbis_genre_is_split_but_id3_numeric_genre_is_ignored()
        {
            var genres = normalizer.NormalizeAll("vorbis", "genre", "Country Music; New Age", ProviderId);
            Assert.Equal(new[] { "Country Music", "New Age" }, genres.Select(a => ((TextAttribute)a).Value));
            // ID3 genre arrives numeric (exiftool -n), so it is deliberately not mapped.
            Assert.Null(normalizer.Normalize("id3", "genre", "255", ProviderId));
        }

        [Fact]
        public void Vorbis_replaygain_handles_db_suffix()
        {
            var gain = (FloatAttribute)normalizer.Normalize("vorbis", "replaygaintrackgain", "-7.16 dB", ProviderId)!;
            Assert.Equal(Audio.TrackGain, gain.AttributeDefinitionId);
            Assert.Equal(-7.16, gain.Value, 3);
        }

        [Fact]
        public void Id3_tags_map_including_band_as_album_artist()
        {
            Assert.Equal(General.Title, Def("id3", "title", "Test Tone"));
            Assert.Equal(Audio.AlbumArtist, Def("id3", "band", "Smokey Mountain"));
            Assert.Equal(General.Year, Def("id3", "year", "1994"));
            Assert.Equal(1994L, ((IntegerAttribute)normalizer.Normalize("id3", "year", "1994", ProviderId)!).Value);
        }

        [Theory]
        [InlineData("productname", "StarCraft Setup")]
        [InlineData("companyname", "Blizzard Entertainment")]
        [InlineData("legalcopyright", "(c) 2022 Blizzard")]
        public void Exe_versioninfo_maps(string key, string value)
            => Assert.Equal(value, ((TextAttribute)normalizer.Normalize("exe", key, value, ProviderId)!).Value);

        [Fact]
        public void Exe_targets_and_version()
        {
            Assert.Equal(General.Product, Def("exe", "productname", "StarCraft Setup"));
            Assert.Equal(General.Publisher, Def("exe", "companyname", "Blizzard"));
            Assert.Equal(Package.Version, Def("exe", "fileversion", "1.18.5.3106"));
        }

        [Theory]
        [InlineData("332", "x86-32")]
        [InlineData("34404", "x86-64")]
        public void Exe_machinetype_maps_pe_code_to_arch(string code, string arch)
        {
            var attr = (TextAttribute)normalizer.Normalize("exe", "machinetype", code, ProviderId)!;
            Assert.Equal(Software.Architecture, attr.AttributeDefinitionId);
            Assert.Equal(arch, attr.Value);
        }

        [Fact]
        public void Exe_machinetype_unknown_code_is_dropped()
            => Assert.Null(normalizer.Normalize("exe", "machinetype", "999", ProviderId));

        [Fact]
        public void Codec_maps_from_compressor_fields()
        {
            Assert.Equal(Media.Codec, Def("xmpdm", "audiocompressor", "MP3"));
            Assert.Equal(Media.Codec, Def("quicktime", "compressorid", "avc1"));
        }
    }
}
