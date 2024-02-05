using System.Numerics;
using System;

namespace Librarian.Utils
{
    public static class HumanizeUtils
    {
        public static string HumanizeSize(long size, int decimals = 2)
        {
            if (size <= 0)
                return size.ToString();

            const string suffixes = " KMGTP";
            int factor = Convert.ToInt32(Math.Floor(Math.Log10(size))) / 3;
            if (factor == 0)
                return size.ToString();
            return string.Format("{0:F" + decimals + "}", size / Math.Pow(1024, factor)) + suffixes[factor];
        }
    }
}
