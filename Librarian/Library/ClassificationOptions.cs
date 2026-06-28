using System;

namespace Librarian.Library
{
    /// <summary>
    /// All the data-driven lists the classifier and folder heuristics use — file-type extensions, MIME
    /// classes, art/role stems, heuristic token lists and name patterns. Loaded at startup from the merged
    /// <c>config.d/*.json</c> (the editable source of truth); <see cref="Default"/> is a built-in fallback
    /// so the app and the tests still work if config is absent or partial. Read-only after startup, so it
    /// is safe to share across the parallel indexing threads.
    /// </summary>
    public class ClassificationOptions
    {
        // ---- Media classes by extension --------------------------------------------------------------
        public string[] VideoExtensions { get; set; } = Array.Empty<string>();
        public string[] AudioExtensions { get; set; } = Array.Empty<string>();
        public string[] ImageExtensions { get; set; } = Array.Empty<string>();
        public string[] ExecutableExtensions { get; set; } = Array.Empty<string>();

        // ---- Media classes by content MIME (preferred when a detected MIME is available) -------------
        public string[] VideoMimePrefixes { get; set; } = Array.Empty<string>();
        public string[] AudioMimePrefixes { get; set; } = Array.Empty<string>();
        public string[] ImageMimePrefixes { get; set; } = Array.Empty<string>();

        // ---- Sidecar / companion roles (name-based) --------------------------------------------------
        public string[] ArtStems { get; set; } = Array.Empty<string>();
        public string[] SubtitleExtensions { get; set; } = Array.Empty<string>();
        public string[] ResourceExtensions { get; set; } = Array.Empty<string>();
        public string[] AuxiliaryExeStems { get; set; } = Array.Empty<string>();

        // ---- Folder→item promotion heuristic ---------------------------------------------------------
        public string[] NoiseTokens { get; set; } = Array.Empty<string>();
        public string[] DiscRipExtensions { get; set; } = Array.Empty<string>();
        public string GenericContentNamePattern { get; set; } = "";

        // ---- Structural-collection naming ------------------------------------------------------------
        public string StructuralNamePattern { get; set; } = "";
        public string SeasonNamePattern { get; set; } = "";

        /// <summary>The built-in defaults — the values the code historically hard-coded. Shipped as the
        /// editable <c>config.d</c> too; this copy keeps tests and a config-less run working.</summary>
        public static ClassificationOptions Default => new()
        {
            VideoExtensions = new[]
            {
                ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".webm", ".wmv", ".flv", ".mpg", ".mpeg",
                ".m2ts", ".mts", ".ts", ".vob", ".ogv", ".3gp", ".divx", ".rm", ".rmvb", ".asf",
            },
            AudioExtensions = new[]
            {
                ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav", ".wma", ".alac", ".ape", ".wv",
            },
            ImageExtensions = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif",
            },
            ExecutableExtensions = new[] { ".exe", ".msi", ".com" },

            VideoMimePrefixes = new[] { "video/" },
            AudioMimePrefixes = new[] { "audio/" },
            ImageMimePrefixes = new[] { "image/" },

            ArtStems = new[]
            {
                "cover", "folder", "backdrop", "logo", "poster", "fanart", "banner",
                "front", "back", "thumb", "thumbnail", "albumart", "albumartsmall", "disc",
            },
            SubtitleExtensions = new[]
            {
                ".srt", ".sub", ".idx", ".ass", ".ssa", ".vtt", ".smi", ".sami", ".sup", ".ttml",
            },
            ResourceExtensions = new[]
            {
                ".dll", ".so", ".dylib", ".ocx", ".sys", ".drv", ".vxd", ".cpl", ".lib", ".a", ".o",
                ".vol", ".ani", ".pak", ".wad", ".dat", ".bin", ".res", ".pk3", ".pk4",
                ".bsp", ".mdl", ".spr", ".vdf", ".gcf", ".big", ".bsa", ".cab",
                ".inf", ".isu", ".ini", ".cfg", ".conf",
            },
            AuxiliaryExeStems = new[]
            {
                "setup", "install", "installer", "autorun", "uninstall", "uninst", "unins000", "unwise",
                "rar", "winrar", "unrar", "7z", "7za", "7zfm", "vcredist", "dxsetup", "dotnetfx",
                "oalinst", "directx", "dxwebsetup", "redist", "update", "patch",
            },

            NoiseTokens = new[]
            {
                "1080p","720p","480p","2160p","4k","uhd","hd","sd","bluray","blu","ray","bdrip","brrip",
                "webrip","web","dl","hdtv","dvdrip","dvd","remux","x264","x265","h264","h265","hevc","avc",
                "xvid","divx","aac","ac3","dts","dd","ddp","mp3","flac","atmos","truehd","10bit","8bit",
                "hdr","sdr","dubbed","subbed","rodubbed","rosub","multi","dual","audio","proper","repack",
                "extended","remastered","unrated","internal","limited","complete","yify","rip",
            },
            DiscRipExtensions = new[] { ".vob", ".ifo", ".bup", ".m2ts", ".bdmv" },
            GenericContentNamePattern =
                @"^(movie|video|film|main|title|playlist|index|stream|default|fullmovie|full|dvd|disc|cd|part|vts[\s._-]*\d+|title[\s._-]*\d+|disc[\s._-]*\d+|cd[\s._-]*\d+|part[\s._-]*\d+|\d+)$",

            StructuralNamePattern =
                @"^(season|series|saison|book|vol|volume|disc|disk|cd|part|pt|s)[\s._-]*\d+$|^(specials?|extras?|bonus)$",
            SeasonNamePattern = @"^(season|series|saison|s)[\s._-]*\d+$|^(specials?|extras)$",
        };
    }
}
