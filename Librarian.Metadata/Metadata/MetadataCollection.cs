using Librarian.Model;
using System.Collections.ObjectModel;

namespace Librarian.Metadata
{
    public class MetadataCollection
    {
        public Collection<MetadataBase> Metadata {  get; } = new Collection<MetadataBase>();

        public Collection<SubResource> SubResources { get; } = new Collection<SubResource>();

        public void Add(MetadataBase metadata)
        {
            Metadata.Add(metadata);
        }

        public void AddSubResource(SubResource resource)
        {
            SubResources.Add(resource);
        }
    }
}
