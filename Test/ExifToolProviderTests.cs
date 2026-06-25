using System.Linq;
using Librarian.Metadata.Providers.ExifTool;
using Xunit;

namespace Test
{
    public class ExifToolProviderTests
    {
        [Fact]
        public void ParseExifToolJson_ReadsFirstObjectScalarTags()
        {
            const string json = """
            [{
              "SourceFile": "/x/a.jpg",
              "EXIF:Make": "Canon",
              "EXIF:ExifImageWidth": 4000,
              "File:ImageWidth": 4000,
              "XMP:Subject": ["a", "b"],
              "EXIF:ThumbnailImage": null
            }]
            """;

            var tags = ExifToolService.ParseExifToolJson(json);

            Assert.NotNull(tags);
            Assert.Equal("Canon", tags!["EXIF:Make"]);
            Assert.Equal("4000", tags["EXIF:ExifImageWidth"]);
            // Structured (array) and null tags are dropped, scalars kept.
            Assert.False(tags.ContainsKey("XMP:Subject"));
            Assert.False(tags.ContainsKey("EXIF:ThumbnailImage"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        [InlineData("[]")]
        public void ParseExifToolJson_ReturnsNullForEmptyOrInvalid(string json)
            => Assert.Null(ExifToolService.ParseExifToolJson(json));

        [Fact]
        public void BuildRawResult_SplitsGroupIntoNamespace()
        {
            var tags = new Dictionary<string, string> { ["EXIF:Make"] = "Canon" };

            var result = ExifToolProvider.BuildRawResult(tags);

            var item = Assert.Single(result.Items);
            Assert.Equal("exif", item.Namespace);
            Assert.Equal("Make", item.Key);
            Assert.Equal("Canon", item.Value);
        }

        [Fact]
        public void BuildRawResult_SkipsFilesystemNoiseToolNamespaceAndBinary()
        {
            var tags = new Dictionary<string, string>
            {
                ["File:FileName"] = "a.jpg",           // filesystem noise -> skipped
                ["File:Directory"] = "/x",             // filesystem noise -> skipped
                ["ExifTool:ExifToolVersion"] = "12.0", // tool bookkeeping namespace -> skipped
                ["EXIF:ThumbnailImage"] = "(Binary data 8902 bytes, use -b option to extract)", // skipped
                ["File:ImageWidth"] = "4000",          // real image fact -> kept
                ["EXIF:Make"] = "Canon",               // kept
            };

            var result = ExifToolProvider.BuildRawResult(tags);

            Assert.Equal(2, result.Items.Count);
            Assert.Contains(result.Items, i => i.Namespace == "file" && i.Key == "ImageWidth");
            Assert.Contains(result.Items, i => i.Namespace == "exif" && i.Key == "Make");
        }

        [Fact]
        public void BuildRawResult_NullTags_ReturnsEmpty()
            => Assert.Empty(ExifToolProvider.BuildRawResult(null).Items);
    }
}
