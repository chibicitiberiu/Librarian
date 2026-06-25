using System.Globalization;
using System.Text.RegularExpressions;

namespace Librarian.Metadata.Normalization
{
    /// <summary>A family of convertible units sharing a canonical base unit.</summary>
    public enum UnitCategory
    {
        /// <summary>Data rate. Canonical base: bits per second (bps).</summary>
        DataRate,
        /// <summary>Data size. Canonical base: byte.</summary>
        DataSize,
        /// <summary>Frequency. Canonical base: hertz (Hz).</summary>
        Frequency,
        /// <summary>Frame rate. Canonical base: frames per second (fps).</summary>
        FrameRate,
    }

    /// <summary>
    /// A small, pure unit catalog + converter for the normalization layer. Numeric metadata often
    /// arrives with a unit suffix ("320 kbps", "4.5 MB", "44.1 kHz") that a plain numeric parse would
    /// reject (and therefore drop). These helpers split the quantity and convert it to its category's
    /// canonical base unit, so a single stored value is always in known units. The per-attribute
    /// <i>display</i> unit lives in the vocabulary's <c>Unit</c> field and matches these bases.
    /// </summary>
    public static class Units
    {
        /// <summary>The canonical base unit a category normalizes to (matches the vocabulary).</summary>
        public static string CanonicalUnit(UnitCategory category) => category switch
        {
            UnitCategory.DataRate => "bps",
            UnitCategory.DataSize => "bytes",
            UnitCategory.Frequency => "Hz",
            UnitCategory.FrameRate => "fps",
            _ => "",
        };

        // Unit token (lower-case) -> (category, multiplicative factor to the category's base unit).
        private static readonly Dictionary<string, (UnitCategory Category, double Factor)> Table =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Data rate (bits per second). SI-style decimal multiples, plus "bit/s" spellings.
                ["bps"] = (UnitCategory.DataRate, 1),
                ["bit/s"] = (UnitCategory.DataRate, 1),
                ["kbps"] = (UnitCategory.DataRate, 1_000),
                ["kbit/s"] = (UnitCategory.DataRate, 1_000),
                ["mbps"] = (UnitCategory.DataRate, 1_000_000),
                ["mbit/s"] = (UnitCategory.DataRate, 1_000_000),
                ["gbps"] = (UnitCategory.DataRate, 1_000_000_000),

                // Data size (bytes). Decimal (KB/MB/...) and binary (KiB/MiB/...) multiples.
                ["b"] = (UnitCategory.DataSize, 1),
                ["byte"] = (UnitCategory.DataSize, 1),
                ["bytes"] = (UnitCategory.DataSize, 1),
                ["kb"] = (UnitCategory.DataSize, 1_000),
                ["mb"] = (UnitCategory.DataSize, 1_000_000),
                ["gb"] = (UnitCategory.DataSize, 1_000_000_000),
                ["tb"] = (UnitCategory.DataSize, 1_000_000_000_000),
                ["kib"] = (UnitCategory.DataSize, 1024d),
                ["mib"] = (UnitCategory.DataSize, 1024d * 1024),
                ["gib"] = (UnitCategory.DataSize, 1024d * 1024 * 1024),
                ["tib"] = (UnitCategory.DataSize, 1024d * 1024 * 1024 * 1024),

                // Frequency.
                ["hz"] = (UnitCategory.Frequency, 1),
                ["khz"] = (UnitCategory.Frequency, 1_000),
                ["mhz"] = (UnitCategory.Frequency, 1_000_000),
                ["ghz"] = (UnitCategory.Frequency, 1_000_000_000),

                // Frame rate.
                ["fps"] = (UnitCategory.FrameRate, 1),
            };

        private static readonly Regex QuantityRegex =
            new(@"^([+-]?[0-9][0-9,]*(?:\.[0-9]+)?)\s*([a-zA-Z/]+)?", RegexOptions.Compiled);

        /// <summary>
        /// Splits a raw quantity into its number and optional unit token, e.g. "320 kbps" ->
        /// (320, "kbps"), "44.1kHz" -> (44.1, "khz"), "1,234" -> (1234, null). Returns false when
        /// there is no leading number.
        /// </summary>
        public static bool TryParseQuantity(string raw, out double number, out string? unit)
        {
            number = 0;
            unit = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var match = QuantityRegex.Match(raw.Trim());
            if (!match.Success)
                return false;

            var numberText = match.Groups[1].Value.Replace(",", "");
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return false;

            if (match.Groups[2].Success && match.Groups[2].Value.Length > 0)
                unit = match.Groups[2].Value.ToLowerInvariant();
            return true;
        }

        /// <summary>
        /// Parses <paramref name="raw"/> and converts it to the canonical base unit of
        /// <paramref name="category"/>. A bare number (no unit) is assumed already in the base unit.
        /// Fails (returns false) for a non-numeric string, an unknown unit, or a unit belonging to a
        /// different category.
        /// </summary>
        public static bool TryNormalize(string raw, UnitCategory category, out double canonical)
        {
            canonical = 0;
            if (!TryParseQuantity(raw, out double number, out string? unit))
                return false;

            if (unit is null)
            {
                canonical = number;
                return true;
            }

            if (!Table.TryGetValue(unit, out var definition) || definition.Category != category)
                return false;

            canonical = number * definition.Factor;
            return true;
        }
    }
}
