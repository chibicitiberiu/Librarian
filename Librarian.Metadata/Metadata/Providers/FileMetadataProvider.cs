using MimeMapping;
using FileAttributes = Librarian.Model.MetadataAttributes.FileAttributes;

namespace Librarian.Metadata.Providers
{
    public class FileMetadataProvider : IMetadataProvider
    {
        private static readonly Guid providerId = new("59f50fc7-230a-4c5f-a526-5aed2c63323f");
        private const string DirectoryMimeType = "inode/directory";
        private const string FileCommand = "/usr/bin/file";

        private readonly MetadataFactory metadataFactory;

        public Guid ProviderId => providerId;

        public string DisplayName => "File attributes";

        public FileMetadataProvider(MetadataFactory metadataFactory)
        {
            this.metadataFactory = metadataFactory;
        }

        public async Task<MetadataCollection> GetMetadataAsync(string filePath)
        {
            MetadataCollection result = new();

            FileSystemInfo? info;
            if (File.Exists(filePath))
            {
                info = new FileInfo(filePath);
                result.Add(metadataFactory.Create(FileAttributes.Size, ((FileInfo)info).Length, ProviderId, editable: false));
                // Content-detected MIME (file --mime-type) is authoritative over the extension — it's what
                // makes classification robust to mismatched extensions; the extension mapping is the
                // fallback when 'file' is unavailable or unsure.
                string mime = await DetectContentMimeAsync(filePath) ?? MimeUtility.GetMimeMapping(filePath);
                result.Add(metadataFactory.Create(FileAttributes.MimeType, mime, ProviderId, editable: false));

                // The human-readable 'file' description (a second call) captures detail a MIME type omits —
                // CPU architecture for binaries, container/codec specifics, encoding, version, etc.
                string? description = await DetectFileDescriptionAsync(filePath);
                if (description != null)
                    result.Add(metadataFactory.Create(FileAttributes.FileType, description, ProviderId, editable: false));
            }
            else if (Directory.Exists(filePath))
            {
                info = new DirectoryInfo(filePath);
                result.Add(metadataFactory.Create(FileAttributes.ItemCount, ((DirectoryInfo)info).EnumerateFileSystemInfos().Count(), ProviderId, editable: false));
                result.Add(metadataFactory.Create(FileAttributes.MimeType, DirectoryMimeType, ProviderId, editable: false));
            }
            else
            {
                throw new IOException("File does not exist!");
            }

            result.Add(metadataFactory.Create(FileAttributes.FileName, info.Name, ProviderId, editable: true));
            result.Add(metadataFactory.Create(FileAttributes.FullPath, info.FullName, ProviderId, editable: false));
            result.Add(metadataFactory.Create(FileAttributes.DateCreated, new DateTimeOffset(info.CreationTime), ProviderId, editable: false));
            result.Add(metadataFactory.Create(FileAttributes.DateModified, new DateTimeOffset(info.LastWriteTime), ProviderId, editable: false));

            return result;
        }

        /// <summary>Content type via libmagic (<c>file --mime-type</c>), or null when 'file' is missing,
        /// errors, or returns something that isn't a "type/subtype" MIME (so the caller falls back to the
        /// extension mapping).</summary>
        private static async Task<string?> DetectContentMimeAsync(string filePath)
        {
            if (!File.Exists(FileCommand))
                return null;

            // '-b' bare output, '-E' surfaces I/O errors as a non-zero exit (instead of "cannot open …" on
            // stdout, which we'd otherwise store as the type), '--mime-type' yields just "type/subtype".
            var (exitCode, output, _) = await ProcessHelper.RunProcessAsync(FileCommand, "-b", "-E", "--mime-type", filePath);
            string mime = output.Trim();
            if (exitCode != 0 || mime.Length == 0 || LooksLikeError(mime) || !mime.Contains('/'))
                return null;
            return mime;
        }

        /// <summary>The human-readable libmagic description (<c>file -b</c>), e.g. "ELF 64-bit LSB
        /// executable, x86-64, …" or "Matroska data". Null when 'file' is missing or errors.</summary>
        private static async Task<string?> DetectFileDescriptionAsync(string filePath)
        {
            if (!File.Exists(FileCommand))
                return null;

            var (exitCode, output, _) = await ProcessHelper.RunProcessAsync(FileCommand, "-b", "-E", filePath);
            string type = output.Trim();
            if (exitCode != 0 || type.Length == 0 || LooksLikeError(type))
                return null;
            return type;
        }

        private static bool LooksLikeError(string fileOutput)
            => fileOutput.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
            || fileOutput.StartsWith("cannot open", StringComparison.OrdinalIgnoreCase)
            || fileOutput.StartsWith("cannot stat", StringComparison.OrdinalIgnoreCase);

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
