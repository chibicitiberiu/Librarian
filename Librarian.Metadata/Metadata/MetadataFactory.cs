using Librarian.DB;
using Librarian.Model;
using Librarian.Util;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Librarian.Metadata
{
    public class MetadataFactory
    {
        readonly DatabaseContext dbContext;
        readonly ILogger logger;

        private readonly static Regex StreamLanguageRegex = new("IAS([0-9]+)", RegexOptions.IgnoreCase);

        public MetadataFactory(DatabaseContext dbContext, ILogger<MetadataFactory> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public MetadataBase Create(MetadataAttributeDefinition definition,
                                   object value,
                                   Guid providerId,
                                   bool editable = false,
                                   SubResource? subResource = null)
        {
            MetadataBase metadata = definition.Type switch
            {
                MetadataType.Text or MetadataType.BigText or MetadataType.FormattedText => new TextMetadata(definition, Convert.ToString(value)!, providerId, editable),
                MetadataType.Integer => new IntegerMetadata(definition, Convert.ToInt64(value), providerId, editable),
                MetadataType.Float => new FloatMetadata(definition, Convert.ToDouble(value), providerId, editable),
                MetadataType.Date => new DateMetadata(definition, ConvertToDateTimeOffset(value), providerId, editable),
                MetadataType.TimeSpan => new FloatMetadata(definition, ConvertToTimeSpan(value), providerId, editable),
                MetadataType.Blob => new BlobMetadata(definition, ConvertToByteArray(value), providerId, editable),
                _ => throw new NotImplementedException(),
            };
            metadata.ProviderId = providerId.ToString();
            metadata.Editable = editable;
            metadata.SubResource = subResource;
            return metadata;
        }

        public MetadataBase? Create(string rawName,
                                    object value,
                                    Guid providerId,
                                    bool editable = false,
                                    SubResource? subResource = null)
        {
            string name = rawName.Trim().ToLower();

            // Find alias
            var attributeAlias = dbContext.AttributeAliases.FirstOrDefault(x => x.Alias == name);

            if (attributeAlias != null)
            {
                if (attributeAlias.AttributeDefinitionId is not null && attributeAlias.AttributeDefinition is null)
                    attributeAlias.AttributeDefinition = dbContext.MetadataAttributes.Find(attributeAlias.AttributeDefinitionId);

                switch (attributeAlias.Role)
                {
                    case AliasRole.Default:
                        if (attributeAlias.AttributeDefinition == null)
                        {
                            logger.LogError("Create {name} (original: '{rawName}') = {value} failed! >> Found alias for attribute {attributeId}, but EF gave me null :(",
                                name, rawName, value, attributeAlias.AttributeDefinitionId);
                            return null;
                        }
                        logger.LogTrace("Create {name} (original: '{rawName}') = {value} >> Found alias for attribute {attributeId} : {attributeGroup} : {attributeName}",
                            name, rawName, value, attributeAlias.AttributeDefinition!.Id, attributeAlias.AttributeDefinition!.Group, attributeAlias.AttributeDefinition!.Name);
                        return Create(attributeAlias.AttributeDefinition!, value, providerId, editable, subResource);

                    case AliasRole.Ignore:
                        logger.LogTrace("Create {name} (original: '{rawName}') = {value} failed >> Alias is ignored!", name, rawName, value);
                        return null;
                }
            }

            // Find existing attribute definition
            string cleanedName = AttributeHelper.NormalizeAttributeName(rawName)!;
            var attributeCandidates = dbContext.MetadataAttributes
                .Where(x => string.Equals(x.Name, cleanedName, StringComparison.OrdinalIgnoreCase))
                .Where(x => MatchesDatatype(value, x))
                .ToArray();

            if (attributeCandidates.Length > 0)
            {
                var attribute = attributeCandidates[0];
                if (attributeCandidates.Length > 1)
                {
                    logger.LogTrace("Create {name} (original: '{rawName}') = {value} >> Found multiple candidate attributes (will pick first):", cleanedName, rawName, value);
                    foreach (var attr in attributeCandidates)
                        logger.LogTrace("* {id} : {group} : {name}", attr.Id, attr.Group, attr.Name);
                }
                else
                {
                    logger.LogTrace("Create {name} (original: '{rawName}') = {value} >> Found attribute {attributeId} : {attributeGroup} : {attributeName}",
                            cleanedName, rawName, value, attribute.Id, attribute.Group, attribute.Name);
                }

                return Create(attribute, value, providerId, editable, subResource);
            }

            // Create attribute
            var newAttribute = new MetadataAttributeDefinition()
            {
                Group = "Other",
                Name = cleanedName,
                Type = GetDatatype(value),
            };

            dbContext.Add(newAttribute);

            logger.LogTrace("Create {name} (original: '{rawName}') = {value} >> New attribute {attributeId} : {attributeGroup} : {attributeName}",
                cleanedName, rawName, value, newAttribute.Id, newAttribute.Group, newAttribute.Name);
            return Create(newAttribute, value, providerId, editable, subResource);
        }

        private static bool MatchesDatatype(object value, MetadataAttributeDefinition attributeDefinition)
        {
            return attributeDefinition.Type switch
            {
                MetadataType.Integer => value.IsInteger() || CanConvertToInteger(value),
                MetadataType.Float => value.IsNumeric() || CanConvertToNumeric(value),
                MetadataType.Date => value is DateTime || value is DateTimeOffset || CanConvertToDatetime(value),
                MetadataType.TimeSpan => value is TimeSpan || CanConvertToTimespan(value),
                MetadataType.Blob => value is byte[],
                MetadataType.Text or MetadataType.BigText or MetadataType.FormattedText => true,
                _ => throw new ArgumentException("Invalid attribute data type"),
            };
        }

        private static MetadataType GetDatatype(object value)
        {
            if (value.IsInteger()) return MetadataType.Integer;
            if (value.IsNumeric()) return MetadataType.Float;
            if (value is DateTime || value is DateTimeOffset) return MetadataType.Date;
            if (value is TimeSpan) return MetadataType.TimeSpan;
            if (value is byte[]) return MetadataType.Blob;
            if (Convert.ToString(value)!.Length > 100) return MetadataType.BigText;
            return MetadataType.Text;
        }

        private static bool CanConvertToInteger(object value)
        {
            return value is string stringValue && long.TryParse(stringValue, out long _);
        }

        private static bool CanConvertToNumeric(object value)
        {
            return value is string stringValue && double.TryParse(stringValue, out double _);
        }

        private static bool CanConvertToDatetime(object value)
        {
            return value is string stringValue && DateTime.TryParse(stringValue, out DateTime _);
        }

        private static bool CanConvertToTimespan(object value)
        {
            return value is string stringValue && TimeSpan.TryParse(stringValue, out TimeSpan _);
        }

        private static DateTimeOffset ConvertToDateTimeOffset(object value)
        {
            if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset;
            if (value is DateTime dateTime) return dateTime;
            throw new ArgumentException("Unsupported conversion from " + value.GetType() + " to DateTimeOffset");
        }

        private static double ConvertToTimeSpan(object value)
        {
            if (value is TimeSpan timeSpan) return timeSpan.TotalSeconds;
            return Convert.ToDouble(value);
        }

        private static byte[] ConvertToByteArray(object value)
        {
            if (value is byte[] bytes) return bytes;
            throw new ArgumentException("Unsupported conversion from " + value.GetType() + " to byte array.");
        }

        public static bool IsStreamLanguageAttribute(string attribute)
        {
            return StreamLanguageRegex.IsMatch(attribute.Trim());
        }

        public static int StreamLanguageGetStreamId(string attribute)
        {
            var match = StreamLanguageRegex.Match(attribute.Trim())!;
            return int.Parse(match.Captures[1].Value);
        }
    }
}
