using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Librarian.Metadata.Archives
{
    /// <summary>
    /// Picks the right <see cref="IArchiveByteSource"/> for an archive and reads entries through it
    /// (collection_plan.md §7.3). Stateless; a single instance serves every archive family that has a
    /// registered source.
    /// </summary>
    public class ArchiveByteReader
    {
        private readonly IReadOnlyList<IArchiveByteSource> sources;

        public ArchiveByteReader(IEnumerable<IArchiveByteSource> sources)
        {
            this.sources = sources.ToList();
        }

        /// <summary>Whether any registered source can read entries from this archive.</summary>
        public bool Supports(string archivePath) => sources.Any(s => s.Supports(archivePath));

        /// <summary>Opens a read stream over one entry's bytes, or null if unsupported/not found.</summary>
        public Stream? OpenEntry(string archivePath, string internalPath)
            => sources.FirstOrDefault(s => s.Supports(archivePath))?.OpenEntry(archivePath, internalPath);
    }
}
