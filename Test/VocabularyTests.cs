using Librarian.Data;
using Librarian.Model;
using Xunit;

namespace Test
{
    /// <summary>
    /// Guards the seed vocabulary (MetadataAttributes.csv) against the data bugs that have bitten
    /// before: a unit string landing on a non-numeric field (e.g. Artist = "dB"), which then surfaced
    /// downstream as a meta-cli `'-4.23 dB'` FormatException. Runs on the parsed dataset, so it also
    /// catches a malformed CSV (bad enum, missing column) at build time.
    /// </summary>
    public class VocabularyTests
    {
        private static readonly AttributeDefinition[] Attributes = Datasets.GetMetadataAttributes().ToArray();

        // Only quantities can carry a unit of measurement. Text/Date/Blob fields must not.
        private static readonly HashSet<AttributeType> UnitBearing =
            new() { AttributeType.Integer, AttributeType.Float, AttributeType.TimeSpan };

        [Fact]
        public void Ids_are_sequential_and_unique_from_one()
        {
            int expected = 1;
            foreach (var attr in Attributes)
                Assert.Equal(expected++, attr.Id);
        }

        [Fact]
        public void Every_attribute_has_a_name_and_group()
        {
            foreach (var attr in Attributes)
            {
                Assert.False(string.IsNullOrWhiteSpace(attr.Name), $"Attribute #{attr.Id} has no name");
                Assert.False(string.IsNullOrWhiteSpace(attr.Group), $"Attribute '{attr.Name}' (#{attr.Id}) has no group");
            }
        }

        [Fact]
        public void Group_and_name_pairs_are_unique()
        {
            var dupes = Attributes
                .GroupBy(a => (a.Group, a.Name))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.Group}/{g.Key.Name}")
                .ToArray();

            Assert.True(dupes.Length == 0, "Duplicate (group, name): " + string.Join(", ", dupes));
        }

        [Theory]
        // Derived/technical facts the user must not author (CSV "TRUE"). Guards the parser regression
        // where a case-sensitive check left every IsReadOnly flag false.
        [InlineData("File attributes", "File type")]
        [InlineData("File attributes", "Size")]
        [InlineData("File attributes", "Checksum")]
        [InlineData("Media", "Duration")]
        [InlineData("Image", "Width")]
        public void Technical_attributes_are_read_only(string group, string name)
        {
            var attr = Attributes.Single(a => a.Group == group && a.Name == name);
            Assert.True(attr.IsReadOnly, $"{group}/{name} should be read-only");
        }

        [Fact]
        public void User_authored_attributes_are_writable()
        {
            foreach (var (group, name) in new[] { ("General", "Title"), ("General", "Description"), ("General", "Tag") })
                Assert.False(Attributes.Single(a => a.Group == group && a.Name == name).IsReadOnly,
                    $"{group}/{name} should be writable");
        }

        [Fact]
        public void Units_only_appear_on_numeric_attributes()
        {
            var offenders = Attributes
                .Where(a => !string.IsNullOrWhiteSpace(a.Unit) && !UnitBearing.Contains(a.Type))
                .Select(a => $"{a.Group}/{a.Name} ({a.Type}) = '{a.Unit}'")
                .ToArray();

            Assert.True(offenders.Length == 0,
                "Non-numeric attributes must not carry a unit: " + string.Join(", ", offenders));
        }
    }
}
