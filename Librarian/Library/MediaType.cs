using System;
using System.Collections.Generic;

namespace Librarian.Library
{
    /// <summary>Coarse media class a file belongs to — the basis for work-centric grouping later.</summary>
    public enum MediaClass
    {
        Audio,
        Video,
        Image,
        Document,
        Software,
        Archive,
        Folder,
        Other,
    }

    /// <summary>
    /// Derives a clean, human "type" (a media class + a friendly label) from a file's MIME signals,
    /// rather than from the freeform <c>file</c> magic string (which yields per-file garbage like
    /// "FLAC … 9838416 samples"). Two MIME signals are reconciled — the file-service MIME (extension
    /// based) and Tika's content type — by preferring the more specific (non-generic) one, which fixes
    /// cases like a Windows <c>.exe</c> reported as <c>application/octet-stream</c> by one and
    /// <c>application/x-msdownload</c> by the other.
    /// </summary>
    public static class MediaType
    {
        public readonly record struct Resolved(MediaClass Class, string Label);

        private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/octet-stream", "binary/octet-stream", "application/x-binary",
            "application/unknown", "content/unknown", "*/*",
        };

        /// <summary>Reconciles the two MIME signals and classifies the result.</summary>
        public static Resolved Resolve(string? fileServiceMime, string? tikaContentType)
        {
            string? mime = BestMime(tikaContentType, fileServiceMime);
            return Classify(mime);
        }

        /// <summary>Prefer a specific MIME over a generic one; Tika wins ties (it is the richer parser).</summary>
        private static string? BestMime(string? tika, string? fileService)
        {
            string? t = Normalize(tika);
            string? f = Normalize(fileService);

            if (t != null && !IsGeneric(t)) return t;
            if (f != null && !IsGeneric(f)) return f;
            return t ?? f;
        }

        private static string? Normalize(string? mime)
        {
            if (string.IsNullOrWhiteSpace(mime))
                return null;
            // Drop parameters like "; charset=utf-8" and lowercase the essence.
            int semi = mime.IndexOf(';');
            string essence = (semi >= 0 ? mime[..semi] : mime).Trim().ToLowerInvariant();
            return essence.Length == 0 ? null : essence;
        }

        private static bool IsGeneric(string mime) => Generic.Contains(mime);

        private static Resolved Classify(string? mime)
        {
            // Null, or a generic placeholder that no parser could improve on → honestly "Unknown".
            if (mime == null || IsGeneric(mime))
                return new(MediaClass.Other, "Unknown");

            if (mime == "inode/directory")
                return new(MediaClass.Folder, "Folder");

            (string type, string sub) = Split(mime);

            return type switch
            {
                "audio" => new(MediaClass.Audio, AudioLabel(sub)),
                "video" => new(MediaClass.Video, VideoLabel(sub)),
                "image" => new(MediaClass.Image, ImageLabel(sub)),
                "text" => new(MediaClass.Document, TextLabel(sub)),
                "application" => Application(sub),
                _ => new(MediaClass.Other, Titleize(sub)),
            };
        }

        private static Resolved Application(string sub) => sub switch
        {
            "pdf" => new(MediaClass.Document, "PDF document"),
            "msword"
                or "vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "vnd.oasis.opendocument.text"
                or "rtf" or "x-rtf" => new(MediaClass.Document, "Word document"),
            "vnd.ms-excel"
                or "vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "vnd.oasis.opendocument.spreadsheet" => new(MediaClass.Document, "Spreadsheet"),
            "vnd.ms-powerpoint"
                or "vnd.openxmlformats-officedocument.presentationml.presentation"
                or "vnd.oasis.opendocument.presentation" => new(MediaClass.Document, "Presentation"),
            "epub+zip" or "x-mobipocket-ebook" or "oebps-package+xml"
                or "vnd.amazon.ebook" or "x-fictionbook+xml" => new(MediaClass.Document, "E-book"),
            "x-msdownload" or "x-dosexec" or "vnd.microsoft.portable-executable"
                or "x-executable" or "x-msi" or "x-ms-installer" => new(MediaClass.Software, "Windows executable"),
            "x-msdos-program" or "x-ms-shortcut" => new(MediaClass.Software, "Windows program"),
            "winhlp" or "x-winhelp" => new(MediaClass.Document, "Windows help"),
            "zip" or "x-rar" or "x-rar-compressed" or "x-7z-compressed"
                or "x-tar" or "gzip" or "x-gzip" or "x-bzip2"
                or "x-zip-compressed" => new(MediaClass.Archive, "Archive"),
            "json" or "xml" or "x-yaml" => new(MediaClass.Document, Titleize(sub)),
            _ => new(MediaClass.Other, Titleize(sub)),
        };

        private static string AudioLabel(string sub) => sub switch
        {
            "x-flac" or "flac" => "FLAC audio",
            "mp3" or "mpeg" or "mpeg3" or "x-mpeg-3" => "MP3 audio",
            "mp4" or "x-m4a" or "aac" => "AAC audio",
            "ogg" or "x-vorbis+ogg" => "Ogg audio",
            "x-wav" or "wav" or "vnd.wave" => "WAV audio",
            "opus" => "Opus audio",
            _ => "Audio",
        };

        private static string VideoLabel(string sub) => sub switch
        {
            "mp4" => "MP4 video",
            "x-matroska" => "Matroska video",
            "x-msvideo" => "AVI video",
            "quicktime" => "QuickTime video",
            "webm" => "WebM video",
            "x-ms-wmv" => "Windows Media video",
            _ => "Video",
        };

        private static string ImageLabel(string sub) => sub switch
        {
            "jpeg" or "jpg" or "pjpeg" => "JPEG image",
            "png" => "PNG image",
            "gif" => "GIF image",
            "bmp" or "x-ms-bmp" => "Bitmap image",
            "webp" => "WebP image",
            "tiff" => "TIFF image",
            "svg+xml" => "SVG image",
            _ => "Image",
        };

        private static string TextLabel(string sub) => sub switch
        {
            "plain" => "Text document",
            "html" or "xhtml" => "HTML document",
            "x-nfo" or "x-nfo+ascii" => "NFO file",
            "markdown" or "x-markdown" => "Markdown document",
            "csv" => "CSV file",
            _ => "Text",
        };

        private static (string Type, string Sub) Split(string mime)
        {
            int slash = mime.IndexOf('/');
            return slash < 0 ? (mime, "") : (mime[..slash], mime[(slash + 1)..]);
        }

        /// <summary>Turns a MIME subtype into a readable fallback label (e.g. "x-doom" → "Doom").</summary>
        private static string Titleize(string sub)
        {
            if (string.IsNullOrEmpty(sub))
                return "Other";

            string s = sub;
            if (s.StartsWith("x-", StringComparison.Ordinal) || s.StartsWith("vnd.", StringComparison.Ordinal))
                s = s[(s.IndexOf('-') + 1)..];
            s = s.Replace('-', ' ').Replace('.', ' ').Replace('+', ' ').Trim();
            if (s.Length == 0)
                return "Other";
            return char.ToUpperInvariant(s[0]) + s[1..];
        }
    }
}
