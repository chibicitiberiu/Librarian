using System;
using Librarian.Metadata.Normalization;
using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Xunit;

namespace Test
{
    /// <summary>
    /// Value-parsing cases taken from a real media library (the NewLibrarian2 TestData dump):
    /// ReplayGain "dB" readings, Matroska HH:MM:SS durations, and "N/M" track/disc forms — all of
    /// which previously either aborted extraction (FormatException) or coerced to a wrong value.
    /// </summary>
    public class RealDataValueParsingTests
    {
        private static readonly Guid ProviderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private readonly MetadataNormalizer normalizer = new();

        [Theory]
        [InlineData("0.00 dB", 0.0)]
        [InlineData("-5.81 dB", -5.81)]
        [InlineData("0.964417", 0.964417)]
        [InlineData("1,234.5", 1234.5)]
        public void Number_coercer_keeps_leading_value_ignoring_unit(string raw, double expected)
        {
            Assert.True(ValueCoercer.Number(raw, out object value));
            Assert.Equal(expected, (double)value, 4);
        }

        [Fact]
        public void TryParseQuantity_handles_db_and_slash_forms()
        {
            Assert.True(Units.TryParseQuantity("-5.81 dB", out double gain, out string? unit));
            Assert.Equal(-5.81, gain, 4);
            Assert.Equal("db", unit);

            // "N/M" track/disc -> leading number (what ConvertToInt64 now stores).
            Assert.True(Units.TryParseQuantity("1/5", out double disc, out _));
            Assert.Equal(1, disc);
        }

        [Fact]
        public void Duration_parses_matroska_hms_string()
        {
            // Real value: streams[n].metadata.DURATION = "01:48:05.563000000"
            Assert.True(ValueCoercer.Duration("01:48:05.563000000", out object value));
            Assert.Equal(6485.563, (double)value, 3);
        }

        [Theory]
        [InlineData("replaygain_track_gain", "-5.81 dB", -5.81)]
        [InlineData("replaygain_track_peak", "0.964417", 0.964417)]
        [InlineData("replaygain_album_gain", "0.00 dB", 0.0)]
        public void Replaygain_is_mapped_from_real_values(string key, string value, double expected)
        {
            var attr = Assert.IsType<FloatAttribute>(normalizer.Normalize("tika", key, value, ProviderId));
            Assert.Equal(expected, attr.Value, 4);
        }

        [Fact]
        public void Replaygain_targets_the_right_attributes()
        {
            Assert.Equal(Audio.TrackGain, normalizer.Normalize("tika", "replaygain_track_gain", "1.0 dB", ProviderId)!.AttributeDefinitionId);
            Assert.Equal(Audio.AlbumPeak, normalizer.Normalize("tika", "replaygain_album_peak", "0.9", ProviderId)!.AttributeDefinitionId);
            Assert.Equal(Audio.ReferenceLoudness, normalizer.Normalize("tika", "replaygain_reference_loudness", "89.0 dB", ProviderId)!.AttributeDefinitionId);
        }
    }
}
