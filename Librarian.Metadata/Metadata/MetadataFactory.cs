using Librarian.DB;
using Librarian.Model;
using Librarian.Util;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
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

        public AttributeBase Create(AttributeDefinition definition,
                                    object value,
                                    Guid? providerId,
                                    string? providerAttributeId = null,
                                    bool editable = false,
                                    SubResource? subResource = null)
        {
            AttributeBase metadata = definition.Type switch
            {
                AttributeType.Text or AttributeType.BigText or AttributeType.FormattedText
                    => new TextAttribute(definition,
                                         value: Convert.ToString(value)!,
                                         providerId: providerId,
                                         providerAttributeId: providerAttributeId,
                                         editable: editable),

                AttributeType.Integer
                    => new IntegerAttribute(definition,
                                            value: Convert.ToInt64(value),
                                            providerId: providerId,
                                            providerAttributeId: providerAttributeId,
                                            editable: editable),

                AttributeType.Float
                    => new FloatAttribute(definition,
                                          value: Convert.ToDouble(value),
                                          providerId: providerId,
                                          providerAttributeId: providerAttributeId,
                                          editable: editable),

                AttributeType.Date
                    => new DateAttribute(definition,
                                         value: ConvertToDateTimeOffset(value),
                                         providerId: providerId,
                                         providerAttributeId: providerAttributeId,
                                         editable: editable),

                AttributeType.TimeSpan
                    => new FloatAttribute(definition,
                                          value: ConvertToTimeSpan(value),
                                          providerId: providerId,
                                          providerAttributeId: providerAttributeId,
                                          editable: editable),

                AttributeType.Blob
                    => new BlobAttribute(definition,
                                         value: ConvertToByteArray(value),
                                         providerId: providerId,
                                         providerAttributeId: providerAttributeId,
                                         editable: editable),

                _ => throw new NotImplementedException(),
            };
            metadata.ProviderId = providerId.ToString();
            metadata.Editable = editable;
            metadata.SubResource = subResource;
            return metadata;
        }

        public AttributeBase Create(string? groupName,
                                    string name,
                                    AttributeType type,
                                    object value,
                                    Guid? providerId,
                                    string? providerAttributeId = null,
                                    bool editable = false,
                                    SubResource? subResource = null)
        {
            var definition = dbContext.AttributeDefinitions
                .FirstOrDefault(x => x.Group == groupName && x.Name == name && x.Type == type);

            if (definition == null)
            {
                definition = new AttributeDefinition(name, groupName, type);
                dbContext.Add(definition);
                dbContext.SaveChanges();
            }

            return Create(definition,
                          value: value,
                          providerId: providerId,
                          providerAttributeId: providerAttributeId,
                          editable: editable,
                          subResource: subResource);
        }

        public AttributeBase? Create(string rawName,
                                     object value,
                                     Guid? providerId,
                                     string? providerAttributeId = null,
                                     bool editable = false,
                                     SubResource? subResource = null)
        {
            string name = rawName.Trim().ToLower();

            // Find alias
            var attributeAlias = dbContext.AttributeAliases.FirstOrDefault(x => x.Alias == name);

            if (attributeAlias != null)
            {
                if (attributeAlias.AttributeDefinitionId is not null && attributeAlias.AttributeDefinition is null)
                    attributeAlias.AttributeDefinition = dbContext.AttributeDefinitions.Find(attributeAlias.AttributeDefinitionId);

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
                        return Create(attributeAlias.AttributeDefinition!, value, providerId, providerAttributeId, editable, subResource);

                    case AliasRole.Ignore:
                        logger.LogTrace("Create {name} (original: '{rawName}') = {value} failed >> Alias is ignored!", name, rawName, value);
                        return null;
                }
            }

            // Find existing attribute definition
            string cleanedName = AttributeHelper.NormalizeAttributeName(rawName)!;
            var attributeCandidates = dbContext.AttributeDefinitions
                .AsEnumerable()
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

                return Create(attribute, value, providerId, providerAttributeId, editable, subResource);
            }

            // Create attribute
            var newAttribute = new AttributeDefinition(cleanedName, "Other", GetDatatype(value));

            dbContext.Add(newAttribute);

            logger.LogTrace("Create {name} (original: '{rawName}') = {value} >> New attribute {attributeId} : {attributeGroup} : {attributeName}",
                cleanedName, rawName, value, newAttribute.Id, newAttribute.Group, newAttribute.Name);
            return Create(newAttribute, value, providerId, providerAttributeId, editable, subResource);
        }

        private static bool MatchesDatatype(object value, AttributeDefinition attributeDefinition)
        {
            return attributeDefinition.Type switch
            {
                AttributeType.Integer => value.IsInteger() || CanConvertToInteger(value),
                AttributeType.Float => value.IsNumeric() || CanConvertToNumeric(value),
                AttributeType.Date => value is DateTime || value is DateTimeOffset || CanConvertToDatetime(value),
                AttributeType.TimeSpan => value is TimeSpan || CanConvertToTimespan(value),
                AttributeType.Blob => value is byte[],
                AttributeType.Text or AttributeType.BigText or AttributeType.FormattedText => true,
                _ => throw new ArgumentException("Invalid attribute data type"),
            };
        }

        private static AttributeType GetDatatype(object value)
        {
            if (value.IsInteger()) return AttributeType.Integer;
            if (value.IsNumeric()) return AttributeType.Float;
            if (value is DateTime || value is DateTimeOffset) return AttributeType.Date;
            if (value is TimeSpan) return AttributeType.TimeSpan;
            if (value is byte[]) return AttributeType.Blob;
            if (Convert.ToString(value)!.Length > 100) return AttributeType.BigText;
            return AttributeType.Text;
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
            string[] formats =
            {
                "yyyy"
            };

            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset;

            if (value is DateTime dateTime)
                return dateTime;

            if (value is string stringValue)
            {
                if (DateTimeOffset.TryParseExact(stringValue, formats, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset result))
                    return result;

                else return DateTimeOffset.Parse(stringValue);
            }

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
