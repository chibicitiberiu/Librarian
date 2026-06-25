using Librarian.Metadata;
using Librarian.Model;
using Xunit;

namespace Test
{
    /// <summary>
    /// Write-side tests for the folder-level <c>.librarian.meta</c> sidecar format. The read/round-trip
    /// path needs the DB-backed <see cref="MetadataFactory"/> (definitions are looked up there) and is
    /// covered by live verification; <see cref="MetadataSerializer.SerializeFolder"/> itself only reads
    /// attribute properties, so it can be exercised without a database.
    /// </summary>
    public class MetadataSerializerTests
    {
        private static MetadataSerializer Serializer() => new(factory: null!);

        private static TextAttribute Text(string group, string name, string value, AttributeType type = AttributeType.Text)
        {
            var def = new AttributeDefinition(name, group, type);
            return new TextAttribute(def, value, providerId: null, editable: true);
        }

        [Fact]
        public void SerializeFolder_is_version_2()
        {
            var doc = Serializer().SerializeFolder(new Dictionary<string, IReadOnlyList<AttributeBase>>());
            Assert.Equal("2", (string)doc.Root!.Attribute("version")!);
        }

        [Fact]
        public void SerializeFolder_writes_archive_entry_locator_keys()
        {
            // An archive entry's override is keyed by its "archive.zip!/internal" locator (§8).
            var files = new Dictionary<string, IReadOnlyList<AttributeBase>>
            {
                ["album.zip!/Disc1/03.flac"] = new AttributeBase[] { Text("General", "Title", "Track 3") },
            };

            var doc = Serializer().SerializeFolder(files);
            var node = doc.Root!.Elements("file").Single();
            Assert.Equal("album.zip!/Disc1/03.flac", (string)node.Attribute("name")!);
        }

        [Fact]
        public void SerializeFolder_writes_collection_elements()
        {
            var collections = new Dictionary<string, IReadOnlyList<AttributeBase>>
            {
                ["My Shows/Tom and Jerry"] = new AttributeBase[] { Text("General", "Title", "Tom & Jerry") },
            };

            var doc = Serializer().SerializeFolder(
                new Dictionary<string, IReadOnlyList<AttributeBase>>(), collections);

            var col = doc.Root!.Elements("collection").Single();
            Assert.Equal("My Shows/Tom and Jerry", (string)col.Attribute("name")!);
            Assert.Equal("Tom & Jerry", col.Elements("text").Single().Value);
        }

        [Fact]
        public void SerializeFolder_writes_one_file_element_per_non_empty_entry()
        {
            var files = new Dictionary<string, IReadOnlyList<AttributeBase>>
            {
                ["book.pdf"] = new AttributeBase[] { Text("General", "Title", "My Book"), Text("General", "Tag", "Computers") },
                ["track.flac"] = new AttributeBase[] { Text("General", "Title", "Song") },
                ["untouched.txt"] = System.Array.Empty<AttributeBase>(),   // no overrides → dropped
            };

            var doc = Serializer().SerializeFolder(files);

            Assert.Equal("librarian", doc.Root!.Name.LocalName);
            var fileNodes = doc.Root.Elements("file").ToList();
            Assert.Equal(2, fileNodes.Count);

            var book = fileNodes.Single(f => (string)f.Attribute("name")! == "book.pdf");
            var texts = book.Elements("text").ToList();
            Assert.Equal(2, texts.Count);
            Assert.Contains(texts, t => (string)t.Attribute("name")! == "Title" && t.Value == "My Book");
            Assert.Contains(texts, t => (string)t.Attribute("name")! == "Tag" && t.Value == "Computers");
        }

        [Fact]
        public void SerializeFolder_records_text_kind_for_big_and_formatted()
        {
            var files = new Dictionary<string, IReadOnlyList<AttributeBase>>
            {
                ["a.txt"] = new AttributeBase[]
                {
                    Text("General", "Description", "desc", AttributeType.FormattedText),
                    Text("General", "Comment", "c", AttributeType.BigText),
                },
            };

            var file = Serializer().SerializeFolder(files).Root!.Element("file")!;

            Assert.Equal("formatted", (string?)file.Elements("text").Single(t => (string)t.Attribute("name")! == "Description").Attribute("kind"));
            Assert.Equal("big", (string?)file.Elements("text").Single(t => (string)t.Attribute("name")! == "Comment").Attribute("kind"));
        }
    }
}
