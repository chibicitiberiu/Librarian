using CsvHelper;
using Librarian.Model;
using System.Globalization;
using System.Reflection;

namespace Librarian.Data
{
    public static class Datasets
    {
        public static IEnumerable<AttributeDefinition> GetMetadataAttributes()
        {
            int index = 1;

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Librarian.Data.MetadataAttributes.csv")!;
            using var streamReader = new StreamReader(stream);
            using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);

            csvReader.Read();
            csvReader.ReadHeader();

            while (csvReader.Read())
            {
                // Accept yes/y/true in any casing (the CSV uses "TRUE"); a case-sensitive `== "true"`
                // previously matched none of them, so every IsReadOnly flag silently seeded as false.
                string readOnlyRaw = (csvReader["IsReadOnly"] ?? string.Empty).Trim();
                bool isReadOnly = readOnlyRaw.StartsWith("y", StringComparison.OrdinalIgnoreCase)
                                  || (bool.TryParse(readOnlyRaw, out bool parsed) && parsed);

                yield return new AttributeDefinition(id: index++,
                                                     name: csvReader["Name"]!,
                                                     group: csvReader["Group"],
                                                     type: Enum.Parse<AttributeType>(csvReader["Type"]!),
                                                     description: csvReader["Description"],
                                                     isReadOnly: isReadOnly,
                                                     unit: csvReader["Unit"]);
            }
        }

        public static IEnumerable<AttributeAlias> GetAliases()
        {
            int index = 1;

            var attributes = GetMetadataAttributes();

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Librarian.Data.MetadataAttributeAliases.csv")!;
            using var streamReader = new StreamReader(stream);
            using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);

            csvReader.Read();
            csvReader.ReadHeader();

            while (csvReader.Read())
            {
                var attributeDefinition = attributes.FirstOrDefault(x => x.Name == csvReader["Name"] && x.Group == csvReader["Group"]);

                if (!Enum.TryParse(csvReader["Role"], out AliasRole role))
                    role = AliasRole.Default;

                yield return new AttributeAlias()
                {
                    Id = index++,
                    Alias = csvReader["Alias"]!.Trim().ToLowerInvariant(),
                    AttributeDefinitionId = attributeDefinition?.Id,
                    // AttributeDefinition = attributeDefinition,
                    Role = role
                };
            }
        }
    }
}
