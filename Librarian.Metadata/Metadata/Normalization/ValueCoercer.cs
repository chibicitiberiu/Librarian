using System.Globalization;

namespace Librarian.Metadata.Normalization
{
    /// <summary>
    /// Value transforms used by the normalization rules. Each coercer turns a raw string
    /// into the CLR value the target attribute expects and is fail-soft: it returns false
    /// instead of throwing, so a single unparseable value never aborts a file's metadata.
    /// The right coercer is chosen per rule (in <see cref="MetadataNormalizer"/>) because
    /// it depends on the source system (e.g. EXIF vs ISO dates).
    /// </summary>
    public static class ValueCoercer
    {
        public delegate bool Coercer(string raw, out object value);

        private static readonly string[] ExifDateFormats =
        {
            "yyyy:MM:dd HH:mm:ss",
            "yyyy:MM:dd HH:mm:sszzz",
            "yyyy:MM:dd",
        };

        private static readonly string[] PlainDateFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            "yyyy-MM",
            "yyyy",
        };

        public static bool Text(string raw, out object value)
        {
            value = raw;
            return true;
        }

        /// <summary>A MIME type with any parameters stripped: "text/plain; charset=ISO-8859-1" and
        /// "application/vnd.wordperfect; version=6.x" both reduce to the bare type. The charset/version
        /// is not part of the content type (and there is no attribute for it), so it is dropped.</summary>
        public static bool MimeType(string raw, out object value)
        {
            string s = raw.Trim();
            int semicolon = s.IndexOf(';');
            if (semicolon >= 0)
                s = s[..semicolon].Trim();
            value = s;
            return s.Length > 0;
        }

        /// <summary>A lenient integer: the leading whole number, ignoring any unit suffix
        /// ("2457 pixels" -> 2457, "16 bits" -> 16). Fail-soft.</summary>
        public static bool IntegerLoose(string raw, out object value)
        {
            if (Units.TryParseQuantity(raw, out double number, out _))
            {
                value = (long)Math.Round(number);
                return true;
            }
            value = default(long);
            return false;
        }

        public static bool Integer(string raw, out object value)
        {
            if (long.TryParse(raw.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long result))
            {
                value = result;
                return true;
            }
            value = default(long);
            return false;
        }

        public static bool Float(string raw, out object value)
        {
            if (double.TryParse(raw.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double result))
            {
                value = result;
                return true;
            }
            value = default(double);
            return false;
        }

        /// <summary>Seconds for a TimeSpan-typed attribute: accepts a number of seconds or HH:MM:SS.</summary>
        public static bool Seconds(string raw, out object value)
        {
            raw = raw.Trim();
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                value = seconds;
                return true;
            }
            if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out TimeSpan ts))
            {
                value = ts.TotalSeconds;
                return true;
            }
            value = default(double);
            return false;
        }

        /// <summary>
        /// Total seconds for a TimeSpan-typed attribute. Supersedes <see cref="Seconds"/>: accepts a
        /// bare/fractional number of seconds (optionally suffixed "s"), the colon forms
        /// <c>[HH:]MM:SS(.fff)</c> (so "2:30.5" -> 150.5), and .NET <see cref="TimeSpan"/> strings
        /// (e.g. "1.02:03:04"). Different providers report durations in all of these forms.
        /// </summary>
        public static bool Duration(string raw, out object value)
        {
            value = default(double);
            raw = raw.Trim();
            if (raw.Length == 0)
                return false;

            // Plain seconds, optionally with a trailing "s" (but not a colon time form).
            string scalar = !raw.Contains(':') && raw.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? raw[..^1].Trim()
                : raw;
            if (double.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                value = seconds;
                return true;
            }

            // Colon forms: [HH:]MM:SS(.fff) — fold each segment in at base 60.
            string[] parts = raw.Split(':');
            if (parts.Length is 2 or 3)
            {
                double total = 0;
                foreach (string part in parts)
                {
                    if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double segment))
                        return false;
                    total = total * 60 + segment;
                }
                value = total;
                return true;
            }

            if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out TimeSpan ts))
            {
                value = ts.TotalSeconds;
                return true;
            }
            return false;
        }

        /// <summary>
        /// A unit-aware integer coercer: parses "&lt;number&gt; &lt;unit&gt;" and converts it to the
        /// canonical base unit of <paramref name="category"/> (a bare number is taken as already in
        /// the base unit), rounding to a whole number. Fail-soft. This is what keeps unit-suffixed
        /// values such as "320 kbps" from being silently dropped.
        /// </summary>
        public static Coercer IntegerIn(UnitCategory category)
            => (string raw, out object value) =>
            {
                if (Units.TryNormalize(raw, category, out double canonical))
                {
                    value = (long)Math.Round(canonical);
                    return true;
                }
                value = default(long);
                return false;
            };

        /// <summary>A unit-aware float coercer (see <see cref="IntegerIn"/>), keeping the fractional
        /// canonical value.</summary>
        public static Coercer FloatIn(UnitCategory category)
            => (string raw, out object value) =>
            {
                if (Units.TryNormalize(raw, category, out double canonical))
                {
                    value = canonical;
                    return true;
                }
                value = default(double);
                return false;
            };

        /// <summary>
        /// A lenient float: the leading numeric quantity, ignoring any trailing unit ("0.00 dB" -> 0.0,
        /// "-5.81 dB" -> -5.81, "0.964417" -> 0.964417). For fields like ReplayGain whose unit (dB) is
        /// already canonical, so only the number is kept. Fail-soft.
        /// </summary>
        public static bool Number(string raw, out object value)
        {
            if (Units.TryParseQuantity(raw, out double number, out _))
            {
                value = number;
                return true;
            }
            value = default(double);
            return false;
        }

        /// <summary>Maps a PE/COFF machine-type code (as exiftool's <c>-n</c> reports it) to a friendly
        /// architecture name matching Tika's <c>machine:machineType</c>, e.g. 332 -> "x86-32",
        /// 34404 -> "x86-64". An unrecognised code fails (so a bare number isn't stored as an arch).</summary>
        public static bool PeMachineType(string raw, out object value)
        {
            value = "";
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                return false;
            string? arch = code switch
            {
                0x014c => "x86-32",     // IMAGE_FILE_MACHINE_I386
                0x8664 => "x86-64",     // AMD64
                0x0200 => "IA-64",
                0xAA64 => "ARM64",
                0x01c0 => "ARM",
                0x01c4 => "ARM-Thumb2",
                _ => null,
            };
            if (arch is null)
                return false;
            value = arch;
            return true;
        }

        /// <summary>ISO-8601 / common date formats (e.g. Dublin Core, Tika).</summary>
        public static bool IsoDate(string raw, out object value)
            => TryDate(raw, PlainDateFormats, out value);

        /// <summary>EXIF date format ("yyyy:MM:dd HH:mm:ss").</summary>
        public static bool ExifDate(string raw, out object value)
            => TryDate(raw, ExifDateFormats, out value);

        private static bool TryDate(string raw, string[] formats, out object value)
        {
            raw = raw.Trim();

            if (DateTimeOffset.TryParseExact(raw, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset exact))
            {
                value = exact;
                return true;
            }

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
            {
                value = parsed;
                return true;
            }

            value = default(DateTimeOffset);
            return false;
        }
    }
}
