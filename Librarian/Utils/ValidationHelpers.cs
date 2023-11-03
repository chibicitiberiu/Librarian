using System;
using System.Globalization;

namespace Librarian.Utils
{
    public static class ValidationExtensions
    {
        public static void ValidateNotNull(this object? @this, string failMessage)
        {
            if (@this == null)
                throw new ArgumentException(failMessage);
        }

        public static void ValidateNotEmpty<T>(this T[]? @this, string failMessage)
        {
            if (@this == null || @this.Length == 0)
                throw new ArgumentException(failMessage);
        }

        public static void ValidateFileName(this string? @this, string failMessage)
        {
            if (string.IsNullOrWhiteSpace(@this))
                throw new ArgumentException(failMessage);

            // make sure name is not a special name
            if (@this == "." || @this == "..")
                throw new ArgumentException(failMessage);

            // make sure name doesn't contain bad characters
            foreach (char c in @this)
            {
                if (c == '\0' || c == '/' || c == '\\')
                    throw new ArgumentException(failMessage);
            }
        }
    }
}
