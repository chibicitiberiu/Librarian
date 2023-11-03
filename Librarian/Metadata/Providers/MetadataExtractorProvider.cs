using MetadataExtractor;
using MetadataExtractor.Formats.Gif;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// Metadata provider based on MetadataExtractor library
    /// </summary>
    public class MetadataExtractorProvider : IMetadataProvider
    {
        public int ProviderId => 0x2000;

        public string DisplayName => "Metadata Extractor";

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

        public void SaveMetadata(string filePath, IEnumerable<MetadataField> metadata)
        {
            throw new System.NotImplementedException();
        }
    }
}
