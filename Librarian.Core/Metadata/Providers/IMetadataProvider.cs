using System.Collections;
using System.Collections.Generic;

namespace Librarian.Metadata.Providers
{
    public interface IMetadataProvider
    {
        int ProviderId { get; }

        string DisplayName { get; }

        IEnumerable<MetadataField> GetMetadata(string filePath);

        void SaveMetadata(string filePath, IEnumerable<MetadataField> metadata);
    }
}
