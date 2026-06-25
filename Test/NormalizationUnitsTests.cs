using Librarian.Metadata.Normalization;
using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Xunit;

namespace Test
{
    /// <summary>Unit normalization: the Units engine, the unit-aware / duration coercers, and the
    /// MetadataNormalizer rules + range validation wired on top of them.</summary>
    public class NormalizationUnitsTests
    {
        private static readonly Guid ProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private readonly MetadataNormalizer normalizer = new();

        // ---- Units engine -----------------------------------------------------------------------

        [Theory]
        [InlineData("320 kbps", 320d, "kbps")]
        [InlineData("44.1kHz", 44.1d, "khz")]
        [InlineData("4.5 MB", 4.5d, "mb")]
        [InlineData("1,234", 1234d, null)]
        [InlineData("96000", 96000d, null)]
        public void TryParseQuantity_splits_number_and_unit(string raw, double expectedNumber, string? expectedUnit)
        {
            Assert.True(Units.TryParseQuantity(raw, out double number, out string? unit));
            Assert.Equal(expectedNumber, number, 3);
            Assert.Equal(expectedUnit, unit);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Stereo")]
        [InlineData("n/a")]
        public void TryParseQuantity_fails_without_leading_number(string raw)
            => Assert.False(Units.TryParseQuantity(raw, out _, out _));

        [Theory]
        [InlineData("320 kbps", UnitCategory.DataRate, 320_000d)]
        [InlineData("1.5 Mbps", UnitCategory.DataRate, 1_500_000d)]
        [InlineData("44.1 kHz", UnitCategory.Frequency, 44_100d)]
        [InlineData("4 MB", UnitCategory.DataSize, 4_000_000d)]
        [InlineData("1 KiB", UnitCategory.DataSize, 1024d)]
        [InlineData("128000", UnitCategory.DataRate, 128_000d)] // bare number = already in base unit
        public void TryNormalize_converts_to_canonical_base(string raw, UnitCategory category, double expected)
        {
            Assert.True(Units.TryNormalize(raw, category, out double canonical));
            Assert.Equal(expected, canonical, 3);
        }

        [Theory]
        [InlineData("44.1 kHz", UnitCategory.DataRate)] // right number, wrong category
        [InlineData("5 furlongs", UnitCategory.DataSize)] // unknown unit
        public void TryNormalize_rejects_wrong_or_unknown_unit(string raw, UnitCategory category)
            => Assert.False(Units.TryNormalize(raw, category, out _));

        // ---- Coercers ---------------------------------------------------------------------------

        [Theory]
        [InlineData("2:30.5", 150.5)]   // M:SS.ms
        [InlineData("1:23:45", 5025)]   // HH:MM:SS
        [InlineData("125.5", 125.5)]    // bare seconds
        [InlineData("90s", 90)]         // seconds with suffix
        public void Duration_coercer_parses_each_form(string raw, double expectedSeconds)
        {
            Assert.True(ValueCoercer.Duration(raw, out object value));
            Assert.Equal(expectedSeconds, (double)value, 3);
        }

        [Fact]
        public void IntegerIn_rounds_to_canonical_base()
        {
            Assert.True(ValueCoercer.IntegerIn(UnitCategory.DataRate)("320 kbps", out object value));
            Assert.Equal(320_000L, (long)value);
        }

        // ---- Normalizer wiring ------------------------------------------------------------------

        [Fact]
        public void Sample_rate_is_unit_aware()
        {
            var khz = (IntegerAttribute)normalizer.Normalize("tika", "samplerate", "44.1 kHz", ProviderId)!;
            Assert.Equal(Audio.SampleRate, khz.AttributeDefinitionId);
            Assert.Equal(44_100L, khz.Value);

            // A bare number still works (already in Hz).
            var hz = (IntegerAttribute)normalizer.Normalize("tika", "samplerate", "48000", ProviderId)!;
            Assert.Equal(48_000L, hz.Value);
        }

        [Fact]
        public void Sample_rate_out_of_range_is_dropped()
        {
            // Above the 192 kHz ceiling -> rejected (would previously have been stored verbatim).
            Assert.Null(normalizer.Normalize("tika", "samplerate", "1000000", ProviderId));
        }

        [Fact]
        public void Bit_rate_converts_kbps_to_bps()
        {
            var bitrate = (IntegerAttribute)normalizer.Normalize("tika", "bitrate", "320 kbps", ProviderId)!;
            Assert.Equal(Media.BitRate, bitrate.AttributeDefinitionId);
            Assert.Equal(320_000L, bitrate.Value);
        }

        [Fact]
        public void Duration_rule_stores_total_seconds()
        {
            var duration = (FloatAttribute)normalizer.Normalize("xmpDM", "duration", "3:07", ProviderId)!;
            Assert.Equal(Media.Duration, duration.AttributeDefinitionId);
            Assert.Equal(187d, duration.Value, 3);
        }

        [Fact]
        public void Frame_rate_out_of_range_is_dropped()
        {
            var ok = (FloatAttribute)normalizer.Normalize("xmpDM", "videoFrameRate", "23.976", ProviderId)!;
            Assert.Equal(Video.FrameRate, ok.AttributeDefinitionId);
            Assert.Equal(23.976d, ok.Value, 3);

            Assert.Null(normalizer.Normalize("xmpDM", "videoFrameRate", "9000", ProviderId));
        }
    }
}
