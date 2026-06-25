using System.IO;

namespace Librarian.Metadata.Archives
{
    /// <summary>
    /// Reads the raw bytes of a single entry out of an archive (collection_plan.md §7.3). Used to hash
    /// archive entries for dedup and to temp-materialize them so path-based providers (ExifTool, meta-cli)
    /// can run on them. One implementation per archive family; the registry picks by archive extension.
    /// </summary>
    public interface IArchiveByteSource
    {
        /// <summary>Whether this source can read entries from the given archive (by extension).</summary>
        bool Supports(string archivePath);

        /// <summary>Opens a read stream over one entry's uncompressed bytes. The caller disposes it.
        /// Returns null if the entry is not found. Throws if the archive cannot be opened.</summary>
        Stream? OpenEntry(string archivePath, string internalPath);
    }
}
