using MetadataExtractor;
using MetadataExtractor.Formats.Gif;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Directory = MetadataExtractor.Directory;

namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// Metadata provider based on MetadataExtractor library
    /// </summary>
    public class MetadataExtractorProvider : IMetadataProvider
    {
        private static readonly Guid providerId = new("c2ebafb5-82d6-4e89-8c9b-863145ee9741");

        public Guid ProviderId => providerId;

        public string DisplayName => "Metadata Extractor";
        /*
        public IEnumerable<MetadataField> GetMetadata(string filePath)
        {
            IReadOnlyList<Directory> directories;
            try
            {
                directories = ImageMetadataReader.ReadMetadata(filePath);
                
            }
            catch (Exception)
            {
                return Enumerable.Empty<MetadataField>();
            }

            List<string> tags = new List<string>();

            foreach (var directory in directories)
            {
                if (directory.HasError)
                    Debug.WriteLine("dir: {0}, errors: {0}", directory.Name, directory.Errors.Aggregate((x, y) => x + ", " + y));

                foreach (var tag in directory.Tags)
                {
                    object? obj = directory.GetObject(tag.Type);
                    tags.Add($"{tag.DirectoryName},{tag.Name},{tag.Description},{obj},{obj?.GetType()}");
                }
            }

            Debug.WriteLine("\n" + tags.Aggregate((x, y) => x + "\n" + y));

            return Enumerable.Empty<MetadataField>();
        }

        public Task<IEnumerable<MetadataField>> GetMetadataAsync(string filePath)
        {
            return Task.FromResult(GetMetadata(filePath));
        }*/

        /*public Task SaveMetadataAsync(string filePath, IEnumerable<MetadataField> metadata)
        {
            throw new NotImplementedException();
        }*/

        public Task SaveMetadataAsync(string filePath, MetadataCollection metadata)
        {
            throw new NotImplementedException();
        }

        Task<MetadataCollection> IMetadataProvider.GetMetadataAsync(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
