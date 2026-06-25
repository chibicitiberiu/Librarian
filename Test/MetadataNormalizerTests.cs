using Librarian.Metadata.Normalization;
using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Xunit;

namespace Test
{
    public class MetadataNormalizerTests
    {
        private static readonly Guid ProviderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private readonly MetadataNormalizer normalizer = new();

        [Fact]
        public void Maps_known_text_key_to_canonical_definition()
        {
            var attribute = normalizer.Normalize("dc", "title", "Sample Page", ProviderId);

            var text = Assert.IsType<TextAttribute>(attribute);
            Assert.Equal(General.Title, text.AttributeDefinitionId);
            Assert.Equal("Sample Page", text.Value);
            Assert.Equal(ProviderId.ToString(), text.ProviderId);
            Assert.Equal("title", text.ProviderAttributeId);
        }

        [Fact]
        public void Key_matching_is_case_insensitive()
        {
            var attribute = normalizer.Normalize("DC", "Title", "X", ProviderId);
            Assert.IsType<TextAttribute>(attribute);
        }

        [Fact]
        public void Maps_iso_date()
        {
            var attribute = normalizer.Normalize("dcterms", "created", "2024-01-27T17:05:04Z", ProviderId);

            var date = Assert.IsType<DateAttribute>(attribute);
            Assert.Equal(General.DateCreated, date.AttributeDefinitionId);
            Assert.Equal(2024, date.Value.Year);
        }

        [Fact]
        public void Uses_source_specific_date_transform()
        {
            // EXIF uses a different date format than ISO; the rule selects the EXIF coercer.
            var attribute = normalizer.Normalize("exif", "DateTimeOriginal", "2024:01:27 17:05:04", ProviderId);

            var date = Assert.IsType<DateAttribute>(attribute);
            Assert.Equal(Media.DateRecorded, date.AttributeDefinitionId);
        }

        [Fact]
        public void Unmapped_key_returns_null()
        {
            Assert.Null(normalizer.Normalize("pdf", "SomethingObscure", "value", ProviderId));
        }

        [Theory]
        // Music (M1): embedded audio tags surfaced by Tika.
        [InlineData("xmpDM", "album", "Destiny")]
        [InlineData("tika", "albumartist", "GUNSHIP")]
        [InlineData("tika", "album artist", "GUNSHIP")] // space-spelt variant
        [InlineData("xmpDM", "artist", "Irene Cara")]
        // Documents / Software (M1).
        [InlineData("dc", "creator", "Andrew S Tanenbaum")]
        [InlineData("machine", "platform", "Windows")]
        [InlineData("machine", "machineType", "x86-32")]
        public void Maps_m1_text_rules(string ns, string key, string value)
        {
            var text = Assert.IsType<TextAttribute>(normalizer.Normalize(ns, key, value, ProviderId));
            Assert.Equal(value, text.Value);
        }

        [Fact]
        public void Promotes_album_artist_variants_to_same_definition()
        {
            var a = (TextAttribute)normalizer.Normalize("tika", "albumartist", "X", ProviderId)!;
            var b = (TextAttribute)normalizer.Normalize("tika", "album artist", "X", ProviderId)!;
            var c = (TextAttribute)normalizer.Normalize("xmpDM", "albumartist", "X", ProviderId)!;
            Assert.Equal(Audio.AlbumArtist, a.AttributeDefinitionId);
            Assert.Equal(Audio.AlbumArtist, b.AttributeDefinitionId);
            Assert.Equal(Audio.AlbumArtist, c.AttributeDefinitionId);
        }

        [Fact]
        public void Maps_track_number_as_integer()
        {
            var integer = Assert.IsType<IntegerAttribute>(normalizer.Normalize("xmpDM", "trackNumber", "5", ProviderId));
            Assert.Equal(Audio.Track, integer.AttributeDefinitionId);
            Assert.Equal(5L, integer.Value);
        }

        [Fact]
        public void Maps_original_year_to_integer_year()
        {
            var integer = Assert.IsType<IntegerAttribute>(normalizer.Normalize("tika", "originalyear", "1983", ProviderId));
            Assert.Equal(General.Year, integer.AttributeDefinitionId);
            Assert.Equal(1983L, integer.Value);
        }

        [Fact]
        public void Splits_multivalued_genre_into_separate_values()
        {
            var attrs = normalizer.NormalizeAll("xmpDM", "genre", "Disco; Pop ; Synthwave; Pop", ProviderId);
            // Split on ';', trimmed and de-duplicated; each a Media.Genre value.
            Assert.Equal(new[] { "Disco", "Pop", "Synthwave" }, attrs.Select(a => ((TextAttribute)a).Value));
            Assert.All(attrs, a => Assert.Equal(Media.Genre, a.AttributeDefinitionId));
        }

        [Fact]
        public void Drops_junk_tags_general_and_language_codes()
        {
            var attrs = normalizer.NormalizeAll("dc", "subject", "Computers; General; ro; Databases; en", ProviderId);
            Assert.Equal(new[] { "Computers", "Databases" }, attrs.Select(a => ((TextAttribute)a).Value));
        }

        [Fact]
        public void Single_valued_field_is_not_split()
        {
            var attrs = normalizer.NormalizeAll("xmpDM", "album", "Greatest Hits; Live", ProviderId);
            Assert.Single(attrs);
            Assert.Equal("Greatest Hits; Live", ((TextAttribute)attrs[0]).Value);
        }

        [Fact]
        public void Maps_software_platform_and_architecture_to_software_group()
        {
            var platform = (TextAttribute)normalizer.Normalize("machine", "platform", "Windows", ProviderId)!;
            var arch = (TextAttribute)normalizer.Normalize("machine", "machineType", "x86-32", ProviderId)!;
            Assert.Equal(Software.Platform, platform.AttributeDefinitionId);
            Assert.Equal(Software.Architecture, arch.AttributeDefinitionId);
        }

        [Fact]
        public void Uncoercible_value_returns_null()
        {
            Assert.Null(normalizer.Normalize("dcterms", "created", "not a date", ProviderId));
        }

        [Fact]
        public void Attaches_sub_resource()
        {
            var sub = new SubResource { Name = "entry", Kind = SubResourceKind.EmbeddedFile };
            var attribute = normalizer.Normalize("dc", "title", "X", ProviderId, sub);

            Assert.Same(sub, attribute!.SubResource);
        }
    }
}
