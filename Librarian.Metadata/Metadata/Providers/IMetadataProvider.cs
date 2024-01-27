using System.Collections;
using System.Collections.Generic;

namespace Librarian.Metadata.Providers
{
    public interface IMetadataProvider
    {
        Guid ProviderId { get; }

        string DisplayName { get; }

        Task<MetadataCollection> GetMetadataAsync(string filePath);

        Task SaveMetadataAsync(string filePath, MetadataCollection metadata);
    }
}
