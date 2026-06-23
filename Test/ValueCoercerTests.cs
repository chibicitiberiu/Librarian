using System.Globalization;
using Librarian.Metadata.Normalization;
using Xunit;

namespace Test
{
    public class ValueCoercerTests
    {
        [Theory]
        [InlineData("42", 42L)]
        [InlineData("  7 ", 7L)]
        [InlineData("1,000", 1000L)]
        public void Integer_parses_valid(string raw, long expected)
        {
            Assert.True(ValueCoercer.Integer(raw, out var value));
            Assert.Equal(expected, (long)value);
        }

        [Theory]
        [InlineData("not a number")]
        [InlineData("1.5")]
        [InlineData("")]
        public void Integer_rejects_invalid(string raw)
        {
            Assert.False(ValueCoercer.Integer(raw, out _));
        }

        [Theory]
        [InlineData("1.5", 1.5)]
        [InlineData("0.25", 0.25)]
        public void Float_uses_invariant_culture(string raw, double expected)
        {
            Assert.True(ValueCoercer.Float(raw, out var value));
            Assert.Equal(expected, (double)value, 6);
        }

        [Fact]
        public void Float_does_not_treat_comma_as_decimal()
        {
            // Invariant culture: "1,5" is parsed as 15 (comma = thousands), never 1.5.
            Assert.True(ValueCoercer.Float("1,5", out var value));
            Assert.Equal(15d, (double)value, 6);
        }

        [Theory]
        [InlineData("90", 90)]
        [InlineData("00:01:30", 90)]
        [InlineData("1.5", 1.5)]
        public void Seconds_accepts_number_or_hms(string raw, double expectedSeconds)
        {
            Assert.True(ValueCoercer.Seconds(raw, out var value));
            Assert.Equal(expectedSeconds, (double)value, 6);
        }

        [Theory]
        [InlineData("2024-01-27T17:05:04Z")]
        [InlineData("2024-01-27 17:05:04")]
        [InlineData("2024-01-27")]
        [InlineData("2024")]
        public void IsoDate_parses_common_formats(string raw)
        {
            Assert.True(ValueCoercer.IsoDate(raw, out var value));
            Assert.Equal(2024, ((DateTimeOffset)value).Year);
        }

        [Fact]
        public void IsoDate_rejects_garbage()
        {
            Assert.False(ValueCoercer.IsoDate("definitely not a date", out _));
        }

        [Fact]
        public void ExifDate_parses_colon_format()
        {
            Assert.True(ValueCoercer.ExifDate("2024:01:27 17:05:04", out var value));
            var date = (DateTimeOffset)value;
            Assert.Equal(new DateTime(2024, 1, 27, 17, 5, 4), date.UtcDateTime);
        }

        [Fact]
        public void Text_passes_through()
        {
            Assert.True(ValueCoercer.Text("anything", out var value));
            Assert.Equal("anything", (string)value);
        }
    }
}
