using Librarian.Model;
using Librarian.Util;
using System.Xml;
using System.Xml.Linq;

namespace Librarian.Metadata
{
    public class MetadataSerializer
    {
        private readonly MetadataFactory factory;

        public MetadataSerializer(MetadataFactory factory)
        {
            this.factory = factory;
        }

        #region Serialization

        public async Task Serialize(string fileName, IEnumerable<MetadataAttributeBase> attributes)
        {
            await using var writer = new StreamWriter(fileName);
            await using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings() { Async = true, Indent = true });

            XDocument document = Serialize(attributes);
            await document.WriteToAsync(xmlWriter, new CancellationToken());
        }

        public XDocument Serialize(IEnumerable<MetadataAttributeBase> attributes)
        {
            var fileAttributes = attributes
                .Where(x => x.SubResource == null)
                .OrderBy(x => x.AttributeDefinition.Group)
                .ThenBy(x => x.AttributeDefinition.Name)
                .Select(SerializeAttribute);

            var rootNode = new XElement("metadata", fileAttributes);

            var subResources = attributes
                .Where(x => x.SubResource != null)
                .GroupBy(x => x.SubResource)
                .Select(grouping =>
                {
                    var subResourceAttributes = grouping
                        .OrderBy(x => x.AttributeDefinition.Group)
                        .ThenBy(x => x.AttributeDefinition.Name)
                        .Select(SerializeAttribute);

                    var xmlSubResource = ToXElement(grouping.Key!);
                    xmlSubResource.Add(subResourceAttributes);
                    return xmlSubResource;
                });

            var subResourcesNode = new XElement("subResources", subResources);
            if (subResourcesNode.HasElements)
                rootNode.Add(subResourcesNode);

            return new XDocument(rootNode);
        }

        private XElement SerializeAttribute(MetadataAttributeBase attribute)
        {
            XElement ret = ToXElement(attribute as dynamic);

            if (attribute.AttributeDefinition.Group is not null)
                ret.Add(new XAttribute("group", attribute.AttributeDefinition.Group));

            ret.Add(new XAttribute("name", attribute.AttributeDefinition.Name));

            if (attribute.Editable)
                ret.Add(new XAttribute("editable", true));

            if (attribute.IsUserEdited)
                ret.Add(new XAttribute("userEdited", true));

            return ret;
        }

        private static XElement ToXElement(TextMetadata attribute)
        {
            var ret = new XElement("text", new XText(attribute.Value));

            if (attribute.AttributeDefinition.Type == MetadataType.BigText)
                ret.Add(new XAttribute("kind", "big"));
            else if (attribute.AttributeDefinition.Type == MetadataType.FormattedText)
                ret.Add(new XAttribute("kind", "formatted"));

            return ret;
        }

        private static XElement ToXElement(IntegerMetadata attribute)
        {
            return new XElement("int", new XText(attribute.Value.ToString()));
        }

        private static XElement ToXElement(FloatMetadata attribute)
        {
            if (attribute.AttributeDefinition.Type == MetadataType.TimeSpan)
            {
                TimeSpan ts = TimeSpan.FromSeconds(attribute.Value);
                return new XElement("timeSpan", new XText(ts.ToString()));
            }
            else
            {
                return new XElement("float", new XText(attribute.Value.ToString()));
            }
        }

        private static XElement ToXElement(BlobMetadata attribute)
        {
            string value = Convert.ToBase64String(attribute.Value);
            return new XElement("blob", new XText(value));
        }

        private static XElement ToXElement(DateMetadata attribute)
        {
            return new XElement(name: "date", new XText(attribute.Value.ToString()));
        }

        private static XElement ToXElement(SubResource subResource)
        {
            var ret = new XElement("subResource");

            if (subResource.InternalId.HasValue)
                ret.Add(new XAttribute("id", subResource.InternalId.Value.ToString()));

            ret.Add(new XAttribute("name", subResource.Name));
            ret.Add(new XAttribute("kind", subResource.Kind.ToString().ToLowerInvariant()));

            return ret;
        }

        #endregion

        #region Deserialization

        public async Task<IEnumerable<MetadataAttributeBase>> Deserialize(string fileName)
        {
            using var reader = new StreamReader(fileName);
            XDocument document = await XDocument.LoadAsync(reader, LoadOptions.SetLineInfo, new CancellationToken());
            return Deserialize(document);
        }

        public IEnumerable<MetadataAttributeBase> Deserialize(XDocument document)
        {
            if (document.Root == null)
                throw new MetadataSerializationException(document, "Missing root element!");

            foreach (var child in document.Root.Elements())
            {
                if (child.Name == "subResources")
                {
                    foreach (var xmlSubResource in child.Elements("subResource"))
                    {
                        var subResource = SubResourceFromXElement(xmlSubResource);
                        var subResourceAttributes = xmlSubResource.Elements()
                            .Select(x => AttributeFromXElement(x, subResource));
                        foreach (var attribute in subResourceAttributes)
                            yield return attribute;
                    }
                }
                else
                {
                    var attribute = AttributeFromXElement(child);
                    yield return attribute;
                }
            }
        }

        private MetadataAttributeBase AttributeFromXElement(XElement xmlAttribute, SubResource? subResource = null)
        {
            string name = xmlAttribute.StringAttribute("name", true)!;
            string? group = xmlAttribute.StringAttribute("group");
            bool editable = xmlAttribute.BoolAttribute("editable") ?? false;
            bool userEdited = xmlAttribute.BoolAttribute("userEdited") ?? false;
            MetadataType type = GetAttributeType(xmlAttribute);

            string valueStr = xmlAttribute.Text() ?? string.Empty;

            return factory.Create(group,
                                  name,
                                  type,
                                  valueStr,
                                  providerId: Guid.Empty,
                                  providerAttributeId: null,
                                  editable: editable,
                                  canSaveToFile: true,
                                  subResource: subResource,
                                  isUserEdited: userEdited);
        }

        private static MetadataType GetAttributeType(XElement xmlAttribute)
        {
            var kindAttribute = xmlAttribute.Attribute("kind");
            string? kind = kindAttribute?.Value;

            MetadataType type = xmlAttribute.Name.LocalName switch
            {
                "text" => MetadataType.Text,
                "blob" => MetadataType.Blob,
                "int" => MetadataType.Integer,
                "float" => MetadataType.Float,
                "date" => MetadataType.Date,
                "timeSpan" => MetadataType.TimeSpan,
                _ => throw new MetadataSerializationException(xmlAttribute, "Invalid element type. Expected one of: text, blob, int, float, date, timeSpan."),
            };

            if (type == MetadataType.Text && kind is not null)
            {
                type = kind.ToLower() switch
                {
                    "big" => MetadataType.BigText,
                    "formatted" => MetadataType.FormattedText,
                    "simple" => MetadataType.Text,
                    _ => throw new MetadataSerializationException(kindAttribute!, "Invalid text kind. Expected one of: simple, big, formatted (default: simple)"),
                };
            }

            return type;
        }

        private static SubResource SubResourceFromXElement(XElement xmlSubResource)
        {
            long? id = xmlSubResource.LongAttribute("id");
            string name = xmlSubResource.StringAttribute("name", true)!;
            SubResourceKind kind = xmlSubResource.EnumAttribute<SubResourceKind>("kind", true)!.Value;

            return new SubResource()
            {
                InternalId = id,
                Name = name,
                Kind = kind
            };
        }

        #endregion
    }
}
