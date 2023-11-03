using Librarian.Model;

namespace Librarian.Metadata
{
    public class MetadataField
    {
        public MetadataAttribute Definition { get; set; }
        public object Value { get; set; } = null!;
        public bool Editable { get; set; }
        public int ProviderId { get; set; }

        public MetadataField(MetadataAttribute definition, object value, bool editable = false, int? providerId = null)
        {
            Definition = definition;
            Value = value;
            Editable = editable;
            ProviderId = providerId ?? -1;
        }
    }
}
