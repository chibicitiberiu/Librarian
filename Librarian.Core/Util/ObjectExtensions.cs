using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Librarian.Util
{
    public static class ObjectExtensions
    {
        public static bool IsNumeric(this object value)
        {
            return value.IsInteger() || value.IsFloatingPoint();
        }

        public static bool IsInteger(this object value)
        {
            return value is byte || value is sbyte
                || value is short || value is ushort
                || value is int || value is uint
                || value is long || value is ulong
                || value is nint || value is nuint;
        }

        public static bool IsFloatingPoint(this object value)
        {
            return value is float || value is double || value is decimal;
        }
    }
}
