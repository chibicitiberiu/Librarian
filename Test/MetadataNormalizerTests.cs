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
