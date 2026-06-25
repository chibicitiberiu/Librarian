using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Librarian.Metadata.Archives
{
    /// <summary>
    /// Reads entries from ZIP-family archives (.zip, .cbz) using the built-in
    /// <see cref="System.IO.Compression"/> — no third-party dependency. Other archive families (tar, 7z,
    /// rar) need their own <see cref="IArchiveByteSource"/>; until then their entries keep the metadata
    /// Tika already extracted but are not re-hashed/re-materialized.
    /// </summary>
    public class ZipArchiveByteSource : IArchiveByteSource
    {
        public bool Supports(string archivePath)
        {
            string ext = Path.GetExtension(archivePath);
            return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase);
        }

        public Stream? OpenEntry(string archivePath, string internalPath)
        {
            // Open the archive read-only and copy the entry to a memory buffer: the ZipArchive must stay
            // open while its entry stream is read, and returning a buffer frees the caller from managing
            // the archive's lifetime. Entries are size-gated (≥ 1 MiB only) so buffering is bounded.
            var archive = ZipFile.OpenRead(archivePath);
            try
            {
                var entry = FindEntry(archive, internalPath);
                if (entry is null)
                    return null;

                var buffer = new MemoryStream();
                using (var entryStream = entry.Open())
                    entryStream.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
            finally
            {
                archive.Dispose();
            }
        }

        private static ZipArchiveEntry? FindEntry(ZipArchive archive, string internalPath)
        {
            // ZipArchiveEntry.FullName uses forward slashes; our InternalPath is already normalized so.
            return archive.Entries.FirstOrDefault(e =>
                       string.Equals(Normalize(e.FullName), internalPath, StringComparison.Ordinal))
                ?? archive.Entries.FirstOrDefault(e =>
                       string.Equals(Normalize(e.FullName), internalPath, StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    }
}
