using System;
using System.IO;

namespace Librarian.Utils
{
    public static class PathUtils
    {
        public static string GetCanonicalPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return new Uri(fullPath).LocalPath;
        }

        public static Uri GetCanonicalUri(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return new Uri(fullPath);
        }

    }
}
