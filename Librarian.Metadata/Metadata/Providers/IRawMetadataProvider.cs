namespace Librarian.Metadata.Providers
{
    /// <summary>
    /// A metadata provider that emits raw, un-normalized (namespace, key, value) records.
    /// These are persisted as the raw layer and promoted to canonical attributes by the
    /// MetadataNormalizer, so adding a provider does not require it to know the canonical
    /// vocabulary.
    /// </summary>
    public interface IRawMetadataProvider
    {
        Guid ProviderId { get; }

        string DisplayName { get; }

        Task<RawMetadataResult> GetRawMetadataAsync(string filePath);
    }
}
