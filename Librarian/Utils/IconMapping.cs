using System.Collections.Generic;
using System.IO;

namespace Librarian.Utils
{
    public static class IconMapping
    {
        public const string DefaultIcon = "~/icons/16/file-types/file-generic.png";
        public const string FolderIcon = "~/icons/16/file-types/folder.png";

        private const string BaseDir = "~/icons/16/file-types/";

        private static readonly Dictionary<string, string> ByExtension = new()
        {
            { "exe", "application-x-executable.png" },
            { "com", "application-x-executable.png" },
            { "dmg", "application-x-executable.png" },

            { "bat", "text-x-script.png" },
            { "cmd", "text-x-script.png" },
            { "sh", "text-x-script.png" },

            { "iso", "media-optical.png" },
            { "cue", "media-optical.png" },
            { "nrg", "media-optical.png" },
        };

        private static readonly Dictionary<string, string> ByMimeType = new()
        {
            { "application/zip", "package-x-generic.png" },
            { "audio", "audio-x-generic.png" },
            { "text", "text-x-generic.png" },
            { "text/html", "text-html.png" },
            { "image", "image-x-generic.png" },
            { "video", "video-x-generic.png" }
        };

        public static string GetIconUrl(string fileName, string mimeType)
        {
            string ext = Path.GetExtension(fileName.ToLower()).Trim('.');
            string mimeShort = mimeType.Substring(0, mimeType.IndexOf("/"));

            string? image = ByExtension.GetValueOrDefault(ext)
                            ?? ByMimeType.GetValueOrDefault(mimeType)
                            ?? ByMimeType.GetValueOrDefault(mimeShort);

            return image == null ? DefaultIcon : (BaseDir + image);
        }
    }
}
