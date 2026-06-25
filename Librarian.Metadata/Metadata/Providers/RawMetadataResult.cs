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

    /// <summary>
    /// A file embedded inside a container (an archive entry, a mail attachment, ...) as reported by a
    /// provider that unpacks containers (Tika). Unlike a <see cref="SubResource"/> — which is a genuine
    /// intra-file part (a stream/chapter) glued to its parent — an embedded resource is a whole file that
    /// the catalog materializes as its own virtual <see cref="IndexedFile"/> when its container is an
    /// archive (collection_plan.md §3.1, §7).
    /// </summary>
    public class EmbeddedResource
    {
        /// <summary>Path of this entry within its container, e.g. "Disc1/03.flac". The locator.</summary>
        public string InternalPath { get; }

        /// <summary>The entry's own raw metadata (entry-level; never carries a sub-resource).</summary>
        public List<RawMetadataItem> Items { get; } = new();

        /// <summary>The entry's extracted text content, if any.</summary>
        public string? Content { get; set; }

        /// <summary>The entry's uncompressed size, if the provider reported it.</summary>
        public long? Size { get; set; }

        public EmbeddedResource(string internalPath)
        {
            InternalPath = internalPath;
        }

        public void Add(string @namespace, string key, string value)
            => Items.Add(new RawMetadataItem(@namespace, key, value));
    }

    /// <summary>The raw metadata extracted from a file by an <see cref="IRawMetadataProvider"/>.</summary>
    public class RawMetadataResult
    {
        public List<RawMetadataItem> Items { get; } = new();
        public List<SubResource> SubResources { get; } = new();

        /// <summary>Files embedded within this one (archive entries, ...). The catalog explodes these into
        /// their own virtual files when the container is an archive (collection_plan.md §7).</summary>
        public List<EmbeddedResource> Embedded { get; } = new();

        /// <summary>Extracted text content of the file, if the provider produced any.</summary>
        public string? Content { get; set; }

        public void Add(string @namespace, string key, string value, SubResource? subResource = null)
            => Items.Add(new RawMetadataItem(@namespace, key, value, subResource));

        public void AddSubResource(SubResource subResource) => SubResources.Add(subResource);

        public void AddEmbedded(EmbeddedResource resource) => Embedded.Add(resource);
    }
}
