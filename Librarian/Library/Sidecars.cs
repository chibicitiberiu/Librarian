using System;
using System.Collections.Generic;
using System.IO;

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
        /// game data file, installer/config (<c>.dll</c>, <c>.vol</c>, <c>.ani</c>, <c>.inf</c>, …).</summary>
        CompanionResource,
    }

    /// <summary>
    /// Recognises sidecar/companion files by name, so the association service can fold them into their
    /// primary work instead of letting them surface as standalone library items. Purely name-based —
    /// it is deliberately conservative (only well-known conventions) to avoid hijacking real content.
    /// </summary>
    public static class Sidecars
    {
        private static readonly HashSet<string> ArtStems = new(StringComparer.OrdinalIgnoreCase)
        {
            "cover", "folder", "backdrop", "logo", "poster", "fanart", "banner",
            "front", "back", "thumb", "thumbnail", "albumart", "albumartsmall", "disc",
        };

        private static readonly HashSet<string> ArtExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif",
        };

        // Extensions that are never a library item on their own — they only exist to serve a primary.
        private static readonly HashSet<string> ResourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // shared libraries / binary components
            ".dll", ".so", ".dylib", ".ocx", ".sys", ".drv", ".vxd", ".cpl", ".lib", ".a", ".o",
            // game / application data
            ".vol", ".ani", ".pak", ".wad", ".dat", ".bin", ".res", ".pk3", ".pk4",
            ".bsp", ".mdl", ".spr", ".vdf", ".gcf", ".big", ".bsa", ".cab",
            // install / config leftovers
            ".inf", ".isu", ".ini", ".cfg", ".conf",
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav", ".wma", ".alac", ".ape", ".wv",
        };

        private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".com",
        };

        public static SidecarKind Classify(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);

            if (ext.Equals(".opf", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Opf;
            if (ext.Equals(".lrc", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Lrc;
            if (ext.Equals(".nfo", StringComparison.OrdinalIgnoreCase))
                return SidecarKind.Nfo;
            if (ArtExtensions.Contains(ext) && ArtStems.Contains(stem))
                return SidecarKind.CompanionArt;
            if (ResourceExtensions.Contains(ext))
                return SidecarKind.CompanionResource;

            return SidecarKind.Content;
        }

        public static bool IsImage(string fileName) => ArtExtensions.Contains(Path.GetExtension(fileName));
        public static bool IsAudio(string fileName) => AudioExtensions.Contains(Path.GetExtension(fileName));
        public static bool IsExecutable(string fileName) => ExecutableExtensions.Contains(Path.GetExtension(fileName));

        // Executables that ship alongside an app but are not the app itself — installers, archivers,
        // uninstallers, redistributables. Used to avoid picking them as a bundle's primary.
        private static readonly HashSet<string> AuxiliaryExeStems = new(StringComparer.OrdinalIgnoreCase)
        {
            "setup", "install", "installer", "autorun", "uninstall", "uninst", "unins000", "unwise",
            "rar", "winrar", "unrar", "7z", "7za", "7zfm", "vcredist", "dxsetup", "dotnetfx",
            "oalinst", "directx", "dxwebsetup", "redist", "update", "patch",
        };

        /// <summary>True for a helper executable (installer/archiver/redist) that should not be
        /// chosen as a bundle's primary app.</summary>
        public static bool IsAuxiliaryExecutable(string fileName)
        {
            if (!IsExecutable(fileName))
                return false;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return AuxiliaryExeStems.Contains(stem)
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
