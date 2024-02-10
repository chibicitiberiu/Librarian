using Librarian.Model;
using System.Collections.ObjectModel;

namespace Librarian.Metadata
{
    public class MetadataCollection
    {
        public Collection<AttributeBase> Metadata {  get; } = new Collection<AttributeBase>();

        public Collection<SubResource> SubResources { get; } = new Collection<SubResource>();

        public void Add(AttributeBase metadata)
        {
            Metadata.Add(metadata);
        }

        public void AddSubResource(SubResource resource)
        {
            SubResources.Add(resource);
        }
    }
}
