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
