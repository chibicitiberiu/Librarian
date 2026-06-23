using Librarian.Model;
using Librarian.Model.MetadataAttributes;
using Microsoft.Extensions.Logging;

namespace Librarian.Metadata.Normalization
{
    /// <summary>
    /// Promotes raw metadata (namespace + key + value) into canonical, typed attributes.
    /// The mapping rules and their value transforms live here in code rather than in data:
    /// the correct transform often depends on the source system, so keeping it next to the
    /// mapping makes the rules easy to read, refactor and unit-test, and avoids a database
    /// migration every time a rule changes. Unmapped keys return null and remain in the raw
    /// layer for later curation.
    /// </summary>
    public class MetadataNormalizer
    {
        private delegate AttributeBase AttributeBuilder(int definitionId, object value);

        private record Rule(int DefinitionId, ValueCoercer.Coercer Coercer, AttributeBuilder Build);

        private readonly Dictionary<(string Namespace, string Key), Rule> rules = new();
        private readonly ILogger<MetadataNormalizer>? logger;

        public MetadataNormalizer(ILogger<MetadataNormalizer>? logger = null)
        {
            this.logger = logger;
            RegisterRules();
        }

        /// <summary>
        /// Maps a single raw value to a canonical attribute, or null if there is no rule for
        /// the (namespace, key) pair or the value cannot be coerced to the target type.
        /// </summary>
        public AttributeBase? Normalize(string @namespace, string key, string value, Guid providerId, SubResource? subResource = null)
        {
            if (!rules.TryGetValue((Normalize(@namespace), Normalize(key)), out var rule))
                return null;

            if (!rule.Coercer(value, out object coerced))
            {
                logger?.LogTrace("Could not coerce value '{value}' for {namespace}:{key}", value, @namespace, key);
                return null;
            }

            var attribute = rule.Build(rule.DefinitionId, coerced);
            attribute.ProviderId = providerId.ToString();
            attribute.ProviderAttributeId = key;
            attribute.Editable = true;
            attribute.SubResource = subResource;
            return attribute;
        }

        /// <summary>True if a promotion rule exists for the given (namespace, key).</summary>
        public bool IsMapped(string @namespace, string key)
            => rules.ContainsKey((Normalize(@namespace), Normalize(key)));

        #region Rules

        // The mapping from a source's raw keys to canonical attributes. A custom coercer is
        // supplied only when the source needs special parsing (e.g. EXIF dates).
        private void RegisterRules()
        {
            // Dublin Core / Tika
            Text("dc", "title", General.Title);
            Text("dc", "publisher", General.Publisher);
            Text("dc", "description", General.Description);
            Text("dc", "language", General.Language);
            Text("dc", "subject", General.Tag);
            Text("dc", "rights", General.Copyright);
            Date("dcterms", "created", General.DateCreated);
            Date("dcterms", "modified", General.DateReleased);
            Text("tika", "content-type", General.ContentType);

            // EXIF (example of a source-specific date transform living next to the mapping)
            Date("exif", "datetimeoriginal", Media.DateRecorded, ValueCoercer.ExifDate);
        }

        #endregion

        #region Rule helpers

        private void Text(string ns, string key, int definitionId)
            => Add(ns, key, definitionId, ValueCoercer.Text,
                   (id, v) => new TextAttribute { AttributeDefinitionId = id, Value = (string)v });

        private void Integer(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Integer,
                   (id, v) => new IntegerAttribute { AttributeDefinitionId = id, Value = (long)v });

        private void Float(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Float,
                   (id, v) => new FloatAttribute { AttributeDefinitionId = id, Value = (double)v });

        private void Date(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.IsoDate,
                   (id, v) => new DateAttribute { AttributeDefinitionId = id, Value = (DateTimeOffset)v });

        private void Add(string ns, string key, int definitionId, ValueCoercer.Coercer coercer, AttributeBuilder build)
            => rules[(Normalize(ns), Normalize(key))] = new Rule(definitionId, coercer, build);

        #endregion

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
