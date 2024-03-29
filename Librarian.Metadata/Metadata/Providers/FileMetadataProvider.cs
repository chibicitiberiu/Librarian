﻿using MimeMapping;
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
                result.Add(metadataFactory.Create(FileAttributes.MimeType, MimeUtility.GetMimeMapping(filePath), ProviderId, editable: false));
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
            await GetFileType(filePath, result);

            return result;
        }

        private async Task GetFileType(string filePath, MetadataCollection result)
        {
            if (File.Exists(FileCommand))
            {
                var (exitCode, output, _) = await ProcessHelper.RunProcessAsync(FileCommand, "-b", filePath);
                if (exitCode == 0)
                    result.Attributes.Add(metadataFactory.Create(FileAttributes.FileType, output.Trim(), ProviderId, editable: false));
            }
        }

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }
    }
}
