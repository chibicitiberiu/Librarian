using System.Xml.Linq;

namespace Librarian.Metadata
{
    internal static class XElementExtensions
    {
        internal static string? StringAttribute(this XElement element, string attributeName, bool required = false)
        {
            var attribute = element.Attribute(attributeName);
            string? value = attribute?.Value;

            if (value is null && required)
                throw new MetadataSerializationException(element, $"Required attribute '{attributeName}' is missing!");

            return value;
        }

        internal static bool? BoolAttribute(this XElement element, string attributeName, bool required = false)
        {
            var attribute = element.Attribute(attributeName);
            string? value = attribute?.Value;

            if (value is null)
            {
                if (required)
                    throw new MetadataSerializationException(element, $"Required boolean attribute '{attributeName}' is missing!");
                return null;
            }

            if (bool.TryParse(value, out bool result))
                return result;
            throw new MetadataSerializationException(attribute!, $"For attribute '{attributeName}': expected a boolean, got '{value}'");
        }

        internal static long? LongAttribute(this XElement element, string attributeName, bool required = false)
        {
            var attribute = element.Attribute(attributeName);
            string? value = attribute?.Value;

            if (value is null)
            {
                if (required)
                    throw new MetadataSerializationException(element, $"Required long integer attribute '{attributeName}' is missing!");
                return null;
            }

            if (long.TryParse(value, out long result))
                return result;
            throw new MetadataSerializationException(attribute!, $"For attribute '{attributeName}': expected a long integer, got '{value}'");
        }

        internal static TEnum? EnumAttribute<TEnum>(this XElement element, string attributeName, bool required = false) where TEnum : struct, Enum
        {
            var attribute = element.Attribute(attributeName);
            string? value = attribute?.Value;

            if (value is null)
            {
                if (required)
                    throw new MetadataSerializationException(element, $"Required attribute '{attributeName}' is missing!");
                return null;
            }

            if (Enum.TryParse(value, out TEnum result))
                return result;

            throw new MetadataSerializationException(attribute!, $"For attribute '{attributeName}': allowed values are " + string.Join(", ", Enum.GetNames<TEnum>()));
        }
    }
}
