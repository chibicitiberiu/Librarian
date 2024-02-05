using Librarian.Model;
using System.Collections.ObjectModel;

namespace Librarian.Metadata
{
    public class MetadataCollection
    {
        public Collection<MetadataAttributeBase> Metadata {  get; } = new Collection<MetadataAttributeBase>();

        public Collection<SubResource> SubResources { get; } = new Collection<SubResource>();

        public void Add(MetadataAttributeBase metadata)
        {
            Metadata.Add(metadata);
        }

        public void AddSubResource(SubResource resource)
        {
            SubResources.Add(resource);
        }
    }
}
