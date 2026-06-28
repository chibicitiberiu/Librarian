using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Librarian.Library
{
    /// <summary>What a file is by name within its folder, for work association (plan.md).</summary>
    public enum SidecarKind
    {
        /// <summary>Primary content (the book, track, executable, …) — a candidate work.</summary>
        Content,
        /// <summary>A metadata sidecar carrying the work's catalogue info (Calibre <c>.opf</c>).</summary>
        Opf,
        /// <summary>Lyrics for the same-named audio file.</summary>
        Lrc,
        /// <summary>A release-info text file (<c>.nfo</c> / <c>FILE_ID.NFO</c>).</summary>
        Nfo,
        /// <summary>Cover/art resource (<c>cover.jpg</c>, <c>folder.png</c>, <c>backdrop.*</c>, …).</summary>
        CompanionArt,
        /// <summary>A bundled resource that is never standalone library content — a shared library,
        /// game data file, installer/config, subtitle (<c>.dll</c>, <c>.vol</c>, <c>.srt</c>, …).</summary>
        CompanionResource,
    }

    /// <summary>
    /// Recognises sidecar/companion files by name (and, when available, by detected MIME), so the
    /// association service can fold them into their primary work instead of letting them surface as
    /// standalone library items. The lists it consults live in <c>config.d</c> (see
    /// <see cref="ClassificationOptions"/>) and are installed once at startup via <see cref="Configure"/>.
    /// </summary>
    public static class Sidecars
    {
        private static HashSet<string> _artStems = Set(ClassificationOptions.Default.ArtStems);
        private static HashSet<string> _imageExt = Set(ClassificationOptions.Default.ImageExtensions);
        private static HashSet<string> _subtitleExt = Set(ClassificationOptions.Default.SubtitleExtensions);
        private static HashSet<string> _resourceExt = Set(ClassificationOptions.Default.ResourceExtensions);
        private static HashSet<string> _audioExt = Set(ClassificationOptions.Default.AudioExtensions);
        private static HashSet<string> _videoExt = Set(ClassificationOptions.Default.VideoExtensions);
        private static HashSet<string> _exeExt = Set(ClassificationOptions.Default.ExecutableExtensions);
        private static HashSet<string> _auxExeStems = Set(ClassificationOptions.Default.AuxiliaryExeStems);
        private static string[] _videoMime = ClassificationOptions.Default.VideoMimePrefixes;
        private static string[] _audioMime = ClassificationOptions.Default.AudioMimePrefixes;
        private static string[] _imageMime = ClassificationOptions.Default.ImageMimePrefixes;

        private static HashSet<string> Set(IEnumerable<string> values) => new(values, StringComparer.OrdinalIgnoreCase);

        /// <summary>Installs the classification lists (from config.d) — call once at startup. Read-only
        /// afterwards, so the indexing threads can share it without locking.</summary>
        public static void Configure(ClassificationOptions o)
        {
            _artStems = Set(o.ArtStems);
            _imageExt = Set(o.ImageExtensions);
            _subtitleExt = Set(o.SubtitleExtensions);
            _resourceExt = Set(o.ResourceExtensions);
            _audioExt = Set(o.AudioExtensions);
            _videoExt = Set(o.VideoExtensions);
            _exeExt = Set(o.ExecutableExtensions);
            _auxExeStems = Set(o.AuxiliaryExeStems);
            _videoMime = o.VideoMimePrefixes;
            _audioMime = o.AudioMimePrefixes;
            _imageMime = o.ImageMimePrefixes;
        }

        private static bool MimeMatches(string? mime, string[] prefixes) =>
            !string.IsNullOrEmpty(mime) && prefixes.Any(p => mime.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        public static SidecarKind Classify(string fileName, string? mime = null)
        {
            string ext = Path.GetExtension(fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);

            if (ext.Equals(".opf", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Opf;
            if (ext.Equals(".lrc", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Lrc;
            if (ext.Equals(".nfo", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Nfo;
            if (IsImage(fileName, mime) && _artStems.Contains(stem))
                return SidecarKind.CompanionArt;
            if (_subtitleExt.Contains(ext) || _resourceExt.Contains(ext))
                return SidecarKind.CompanionResource;

            return SidecarKind.Content;
        }

        // Media-class predicates: a detected content MIME wins when present (robust to wrong extensions);
        // otherwise we fall back to the extension. Executables have no reliable MIME, so extension only.
        public static bool IsImage(string fileName, string? mime = null) =>
            MimeMatches(mime, _imageMime) || (string.IsNullOrEmpty(mime) && _imageExt.Contains(Path.GetExtension(fileName)));
        public static bool IsAudio(string fileName, string? mime = null) =>
            MimeMatches(mime, _audioMime) || (string.IsNullOrEmpty(mime) && _audioExt.Contains(Path.GetExtension(fileName)));
        public static bool IsVideo(string fileName, string? mime = null) =>
            MimeMatches(mime, _videoMime) || (string.IsNullOrEmpty(mime) && _videoExt.Contains(Path.GetExtension(fileName)));
        public static bool IsExecutable(string fileName) => _exeExt.Contains(Path.GetExtension(fileName));

        /// <summary>The "primary content" media classes — a folder built around one of these treats every
        /// other loose file (stray text, logs, images, …) as a companion of it.</summary>
        public static bool IsPrimaryMedia(string fileName, string? mime = null) =>
            IsAudio(fileName, mime) || IsVideo(fileName, mime) || IsExecutable(fileName);

        /// <summary>True for a helper executable (installer/archiver/redist) that should not be
        /// chosen as a bundle's primary app.</summary>
        public static bool IsAuxiliaryExecutable(string fileName)
        {
            if (!IsExecutable(fileName))
                return false;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return _auxExeStems.Contains(stem)
                || stem.StartsWith("unins", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Folder-name match key: lowercased, stripped of spaces/punctuation, so
        /// "Outpost 2" and "OUTPOST2.EXE" line up.</summary>
        public static string MatchKey(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>Lower-cased file name without extension — used to bind a sidecar to its sibling
        /// (e.g. an <c>.lrc</c> to the track of the same name).</summary>
        public static string Stem(string fileName) =>
            Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
    }
}
