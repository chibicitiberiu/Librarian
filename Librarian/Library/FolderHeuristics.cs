using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Librarian.Library
{
    /// <summary>
    /// Heuristics that decide whether a folder holding a single content item is *dedicated* to it — so the
    /// folder renders AS that item (e.g. "Inception 2 (2029)/Inception 2.mkv") — versus being a container
    /// that should become a one-item collection (a section folder like "Movies" that merely holds one item
    /// right now). The token lists and patterns it uses live in <c>config.d</c> (see
    /// <see cref="ClassificationOptions"/>), installed once at startup via <see cref="Configure"/>. Kept as
    /// pure functions so they can be unit-tested against the many real-world naming edge cases.
    /// </summary>
    public static class FolderHeuristics
    {
        private static Regex _genericName = Compile(ClassificationOptions.Default.GenericContentNamePattern);
        private static HashSet<string> _noise = Set(ClassificationOptions.Default.NoiseTokens);
        private static HashSet<string> _discRipExt = Set(ClassificationOptions.Default.DiscRipExtensions);
        private static Regex _structural = Compile(ClassificationOptions.Default.StructuralNamePattern);
        private static Regex _season = Compile(ClassificationOptions.Default.SeasonNamePattern);

        private static HashSet<string> Set(IEnumerable<string> v) => new(v, StringComparer.OrdinalIgnoreCase);
        private static Regex Compile(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Installs the heuristic lists/patterns (from config.d) — call once at startup.</summary>
        public static void Configure(ClassificationOptions o)
        {
            _genericName = Compile(o.GenericContentNamePattern);
            _noise = Set(o.NoiseTokens);
            _discRipExt = Set(o.DiscRipExtensions);
            _structural = Compile(o.StructuralNamePattern);
            _season = Compile(o.SeasonNamePattern);
        }

        /// <summary>A folder/archive-subdir name like "Season 1", "Disc 2", "Vol. 3", "Specials" — the
        /// signal that a content folder is one structural level of a larger work.</summary>
        public static bool IsStructuralName(string name) => _structural.IsMatch(name.Trim());

        /// <summary>A season-or-equivalent level ("Season 1", "Specials", "Extras") — makes its parent a Show.</summary>
        public static bool IsSeasonName(string name) => _season.IsMatch(name.Trim());

        /// <summary>Title tokens of a name: split on non-alphanumerics, lowercased, with 4-digit years and
        /// known release/format noise removed.</summary>
        public static List<string> TitleTokens(string name)
        {
            var tokens = new List<string>();
            foreach (var raw in Regex.Split(name ?? "", @"[^\p{L}\p{N}]+"))
            {
                if (raw.Length == 0) continue;
                string t = raw.ToLowerInvariant();
                if (_noise.Contains(t)) continue;
                if (t.Length == 4 && t.All(char.IsDigit)) continue;   // a year, e.g. (2009)
                tokens.Add(t);
            }
            return tokens;
        }

        /// <summary>True when a single-content folder is dedicated to its item (so the folder renders AS the
        /// item) rather than being a container collection. Combines a disc-rip check, a generic-filename
        /// fallback, and folder→file title-token overlap.</summary>
        public static bool FolderIsDedicatedToItem(string folderName, string primaryFileName,
                                                   IEnumerable<string> folderFileNames)
        {
            // 1) Disc rip (VIDEO_TS.IFO / *.VOB / BDMV) lying in the folder → the folder is the disc.
            if (folderFileNames.Any(n => _discRipExt.Contains(Path.GetExtension(n))))
                return true;

            string stem = Path.GetFileNameWithoutExtension(primaryFileName);

            // 2) Generic filename ("movie.mkv", "VTS_01_1.vob") → the descriptive folder is the title.
            if (_genericName.IsMatch(stem.Trim()))
                return true;

            // 3) The folder's (clean) title tokens nearly all appear in the file name → the folder names
            //    this item. Comparing folder→file (not the reverse) means release-scene noise in the file
            //    ("1080p.BluRay.x264-GROUP") doesn't dilute the match, while a container folder whose name
            //    isn't in the file (e.g. "Movies", "Filme") still fails.
            var fileTokens = new HashSet<string>(TitleTokens(stem));
            if (fileTokens.Count == 0)
                return true;   // filename is pure noise → trust the folder name
            var folderTokens = TitleTokens(folderName);
            if (folderTokens.Count == 0)
                return false;  // folder name carries no title (only a year / noise) — can't confirm
            int hits = folderTokens.Count(fileTokens.Contains);
            return hits >= (int)Math.Ceiling(folderTokens.Count * 0.6);
        }
    }
}
