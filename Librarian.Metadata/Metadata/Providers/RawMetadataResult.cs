using Librarian.Model;

namespace Librarian.Metadata.Providers
{
    /// <summary>A single raw (un-normalized) metadata value as reported by a provider.</summary>
    public class RawMetadataItem
    {
        public string Namespace { get; }
        public string Key { get; }
        public string Value { get; }
        public SubResource? SubResource { get; }

        public RawMetadataItem(string @namespace, string key, string value, SubResource? subResource = null)
        {
            Namespace = @namespace;
            Key = key;
            Value = value;
            SubResource = subResource;
        }
    }

    /// <summary>The raw metadata extracted from a file by an <see cref="IRawMetadataProvider"/>.</summary>
    public class RawMetadataResult
    {
        public List<RawMetadataItem> Items { get; } = new();
        public List<SubResource> SubResources { get; } = new();

        /// <summary>Extracted text content of the file, if the provider produced any.</summary>
        public string? Content { get; set; }

        public void Add(string @namespace, string key, string value, SubResource? subResource = null)
            => Items.Add(new RawMetadataItem(@namespace, key, value, subResource));

        public void AddSubResource(SubResource subResource) => SubResources.Add(subResource);
    }
}
