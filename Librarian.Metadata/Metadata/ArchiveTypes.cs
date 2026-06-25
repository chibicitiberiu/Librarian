using System;
using System.Collections.Generic;
using System.IO;

namespace Librarian.Metadata
{
    /// <summary>
    /// Decides whether a file is an archive whose entries should be exploded into the catalog as virtual
    /// files (collection_plan.md §7.1). Detection is by extension — reliable, cheap, and it does not need
    /// the file's bytes. Containers that are not archives (PDFs with embedded fonts, mails with
    /// attachments) deliberately do NOT match: their embedded resources stay un-catalogued.
    /// </summary>
    public static class ArchiveTypes
    {
        // Multi-entry containers whose entries are content worth cataloguing individually. Deliberately
        // excluded:
        //  - bare .gz/.bz2/.xz: a single compressed stream (one file, not a container);
        //  - .jar/.war/.ear (and .apk/.nupkg/etc.): zip-based *executable bundles* — like an .exe, the
        //    unit of interest is the bundle, not its class files, so we don't pick them apart.
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".cbz",
            ".tar",
            ".tgz", ".tbz", ".tbz2", ".txz",
            ".7z", ".cb7",
            ".rar", ".cbr",
        };

        public static bool IsArchive(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName))
                return false;

            string ext = Path.GetExtension(pathOrName);

            // ".tar.gz" / ".tar.bz2" etc. — the compound double extension is still a tarball archive.
            if (ext.Equals(".gz", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".xz", StringComparison.OrdinalIgnoreCase))
            {
                string inner = Path.GetExtension(Path.GetFileNameWithoutExtension(pathOrName));
                if (inner.Equals(".tar", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return Extensions.Contains(ext);
        }
    }
}
