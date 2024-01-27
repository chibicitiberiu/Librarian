using CsvHelper;
using Librarian.Model;
using System.Globalization;
using System.Reflection;

namespace Librarian.Data
{
    public static class Datasets
    {
        public static IEnumerable<MetadataAttributeDefinition> GetMetadataAttributes()
        {
            int index = 1;

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Librarian.Data.MetadataAttributes.csv")!;
            using var streamReader = new StreamReader(stream);
            using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);

            csvReader.Read();
            csvReader.ReadHeader();

            while (csvReader.Read())
            {
                yield return new MetadataAttributeDefinition()
                {
                    Id = index++,
                    Group = csvReader["Group"],
                    Name = csvReader["Name"]!,
                    Description = csvReader["Description"],
                    Type = Enum.Parse<MetadataType>(csvReader["Type"]!)
                };
            }
        }

        public static IEnumerable<MetadataAttributeAlias> GetAliases()
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

                yield return new MetadataAttributeAlias()
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
