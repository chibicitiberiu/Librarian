using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Librarian.Util
{
    public static class StringHelper
    {
        public static string? CleanupInput(string? value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            return value;
        }
    }
}
