using MimeMapping;
using System;
using System.Collections.Generic;
using System.IO;
using FileAttributes = Librarian.Model.MetadataFields.FileAttributes;

namespace Librarian.Metadata.Providers
{
    public class FileMetadataProvider : IMetadataProvider
    {
        public int ProviderId => 0x1000;

        public string DisplayName => "File attributes";

        public IEnumerable<MetadataField> GetMetadata(string filePath)
        {
            FileSystemInfo? info;
            if (File.Exists(filePath))
            {
                info = new FileInfo(filePath);
                yield return new MetadataField(FileAttributes.Size, ((FileInfo)info).Length);
                yield return new MetadataField(FileAttributes.MimeType, MimeUtility.GetMimeMapping(filePath));
            }
            else if (Directory.Exists(filePath))
            {
                info = new DirectoryInfo(filePath);
                yield return new MetadataField(FileAttributes.MimeType, "inode/directory");
            }
            else
            {
                throw new IOException("No such file!");
            }

            yield return new MetadataField(FileAttributes.FileName, info.Name, true);
            yield return new MetadataField(FileAttributes.FullPath, info.FullName);
            yield return new MetadataField(FileAttributes.DateCreated, new DateTimeOffset(info.CreationTime));
            yield return new MetadataField(FileAttributes.DateModified, new DateTimeOffset(info.LastWriteTime));
        }

        public void SaveMetadata(string filePath, IEnumerable<MetadataField> metadata)
        {
            throw new NotImplementedException();
        }
    }
}
