using Librarian.Model;
using System.Collections.ObjectModel;

namespace Librarian.Metadata
{
    public class MetadataCollection
    {
        public Collection<AttributeBase> Attributes {  get; } = new Collection<AttributeBase>();

        public Collection<SubResource> SubResources { get; } = new Collection<SubResource>();

        public void Add(AttributeBase? attribute)
        {
            if (attribute is not null)
                Attributes.Add(attribute);
        }

        public void AddSubResource(SubResource resource)
        {
            SubResources.Add(resource);
        }
    }
}
