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

        private record Rule(int DefinitionId, ValueCoercer.Coercer Coercer, AttributeBuilder Build, bool Multi = false, Func<string, bool>? Filter = null);

        // Characters that separate the values of a multi-valued tag (e.g. "Disco; Pop; Synthwave").
        private static readonly char[] MultiValueSeparators = { ';' };

        private readonly Dictionary<(string Namespace, string Key), Rule> rules = new();
        private readonly ILogger<MetadataNormalizer>? logger;

        public MetadataNormalizer(ILogger<MetadataNormalizer>? logger = null)
        {
            this.logger = logger;
            RegisterRules();
        }

        /// <summary>
        /// Maps a raw value to canonical attribute(s). A multi-valued field (e.g. Genre, Tag) whose
        /// raw value packs several values into one string ("Disco; Pop") yields one attribute per
        /// value; everything else yields zero or one. Empty if there is no rule or nothing coerces.
        /// </summary>
        public IReadOnlyList<AttributeBase> NormalizeAll(string @namespace, string key, string value, Guid providerId, SubResource? subResource = null)
        {
            if (!rules.TryGetValue((Normalize(@namespace), Normalize(key)), out var rule))
                return Array.Empty<AttributeBase>();

            var rawValues = rule.Multi
                ? value.Split(MultiValueSeparators).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct()
                : new[] { value };

            var results = new List<AttributeBase>();
            foreach (var raw in rawValues)
            {
                if (rule.Filter is not null && !rule.Filter(raw))
                    continue;

                if (!rule.Coercer(raw, out object coerced))
                {
                    logger?.LogTrace("Could not coerce value '{value}' for {namespace}:{key}", raw, @namespace, key);
                    continue;
                }

                var attribute = rule.Build(rule.DefinitionId, coerced);
                attribute.ProviderId = providerId.ToString();
                attribute.ProviderAttributeId = key;
                attribute.Editable = true;
                attribute.SubResource = subResource;
                results.Add(attribute);
            }
            return results;
        }

        /// <summary>Maps a raw value to a single canonical attribute (the first, for multi-valued
        /// fields), or null. Convenience over <see cref="NormalizeAll"/>.</summary>
        public AttributeBase? Normalize(string @namespace, string key, string value, Guid providerId, SubResource? subResource = null)
            => NormalizeAll(@namespace, key, value, providerId, subResource).FirstOrDefault();

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
            TextMulti("dc", "subject", General.Tag, IsMeaningfulTag);
            Text("dc", "rights", General.Copyright);
            Date("dcterms", "created", General.DateCreated);
            Date("dcterms", "modified", General.DateReleased);
            Text("tika", "content-type", General.ContentType);

            // EXIF (example of a source-specific date transform living next to the mapping).
            // These keys are emitted by both Tika's image parser and the exiftool provider (which
            // augments it); idempotent canonical writes dedupe any overlap by (definition, value).
            Date("exif", "datetimeoriginal", Media.DateRecorded, ValueCoercer.ExifDate);

            RegisterImageRules();
            RegisterAudioRules();
            RegisterDocumentRules();
            RegisterSoftwareRules();
        }

        // Image dimensions surfaced by the exiftool provider (and Tika). exiftool with -G0 reports
        // decoded dimensions under the generic "File" group (cross-format), and EXIF/PNG carry their
        // own copies; mapping all three covers JPEG/PNG/RAW/etc. Idempotent writes dedupe overlap.
        private void RegisterImageRules()
        {
            Integer("file", "imagewidth", Image.Width);
            Integer("file", "imageheight", Image.Height);
            Integer("exif", "exifimagewidth", Image.Width);
            Integer("exif", "exifimageheight", Image.Height);
            Integer("png", "imagewidth", Image.Width);
            Integer("png", "imageheight", Image.Height);
        }

        // Embedded audio tags surfaced by Tika (xmpDM / tika namespaces). These populate the
        // Music library facets — By Artist, By Album, By Genre, By Year — from data already in
        // the raw layer (re-runnable via the renormalize endpoint, no file re-reads).
        private void RegisterAudioRules()
        {
            Text("xmpdm", "album", Audio.Album);
            Text("xmpdm", "artist", Audio.Artist);
            Integer("xmpdm", "tracknumber", Audio.Track);
            TextMulti("xmpdm", "genre", Media.Genre);
            Date("xmpdm", "releasedate", General.DateReleased);

            // Album artist arrives under three spellings depending on the tag source; a file
            // carries only one, so registering all three is safe.
            Text("tika", "albumartist", Audio.AlbumArtist);
            Text("tika", "album artist", Audio.AlbumArtist);
            Text("xmpdm", "albumartist", Audio.AlbumArtist);

            Integer("tika", "totaltracks", Audio.TotalTracks);
            Integer("tika", "tracktotal", Audio.TotalTracks);
            Integer("tika", "discnumber", Media.Disc);
            Integer("tika", "disc", Media.Disc);
            Integer("tika", "totaldiscs", Media.TotalDiscs);
            Integer("tika", "disctotal", Media.TotalDiscs);

            // "By Year" needs an integer year; originalyear is the plain four-digit value
            // (releasedate above keeps the full date for display).
            Integer("tika", "originalyear", General.Year);

            Text("tika", "composer", Audio.Composer);
            Text("xmpdm", "composer", Audio.Composer);
            Text("tika", "lyrics", Audio.Lyrics);
            Text("tika", "label", Media.Label);
            Text("tika", "copyright", General.Copyright);
            Text("tika", "script", General.Script);
            Text("tika", "releasecountry", General.ReleaseCountry);
            Text("tika", "releasestatus", General.ReleaseStatus);
            Text("tika", "releasetype", General.ReleaseType);
            Text("tika", "catalognumber", General.CatalogNumber);
            Text("tika", "barcode", General.BarCode);
            Text("tika", "asin", General.AmazonStandardIdentificationNumberASIN);

            // MusicBrainz / AcoustID identifiers — handy for later cross-provider reconciliation.
            Text("tika", "musicbrainz_albumid", Media.MusicBrainzAlbumID);
            Text("tika", "musicbrainz_artistid", Media.MusicBrainzArtistID);
            Text("tika", "musicbrainz_albumartistid", Media.MusicBrainzAlbumArtistID);
            Text("tika", "musicbrainz_trackid", Media.MusicBrainzTrackID);
            Text("tika", "musicbrainz_releasegroupid", Media.MusicBrainzReleaseGroupID);
            Text("tika", "musicbrainz_releasetrackid", Media.MusicBrainzReleaseTrackID);
            Text("tika", "acoustid_id", Audio.AcoustIDID);

            // Technical stream facts (one raw source each, to avoid duplicate canonical values).
            Integer("tika", "samplerate", Audio.SampleRate);
            Integer("tika", "channels", Audio.Channels);
            Integer("tika", "bits", Audio.BitsPerSample);
        }

        // Document metadata (Dublin Core via Tika). dc:title/subject/description/language/rights
        // are mapped above; these add author and publisher. For bundled works the author often
        // lives on a sidecar (e.g. a Calibre .opf) — full correctness there waits on sidecar
        // association (plan.md); the rule is harmless and forward-compatible until then.
        private void RegisterDocumentRules()
        {
            Text("dc", "creator", General.WrittenBy);
            Text("tika", "publisher", General.Publisher);
        }

        // Executable / PE header metadata from Tika's machine parser. Populates the Software
        // library facets — By Platform, By Architecture.
        private void RegisterSoftwareRules()
        {
            Text("machine", "platform", Software.Platform);
            Text("machine", "machinetype", Software.Architecture);
        }

        #endregion

        #region Rule helpers

        private void Text(string ns, string key, int definitionId)
            => Add(ns, key, definitionId, ValueCoercer.Text,
                   (id, v) => new TextAttribute { AttributeDefinitionId = id, Value = (string)v });

        /// <summary>A text field whose raw value may pack several values into one string (e.g. a
        /// semicolon-separated genre list) — each becomes its own canonical value. An optional
        /// <paramref name="filter"/> drops junk values (e.g. language codes among tags).</summary>
        private void TextMulti(string ns, string key, int definitionId, Func<string, bool>? filter = null)
            => Add(ns, key, definitionId, ValueCoercer.Text,
                   (id, v) => new TextAttribute { AttributeDefinitionId = id, Value = (string)v }, multi: true, filter: filter);

        /// <summary>Rejects tag values that carry no browsing meaning: the generic "General", and
        /// bare two-letter language codes (e.g. "ro", "en") that ebook exporters dump into subjects.</summary>
        private static bool IsMeaningfulTag(string value)
        {
            string v = value.Trim();
            if (v.Length == 0)
                return false;
            if (v.Equals("general", StringComparison.OrdinalIgnoreCase) || v.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                return false;
            if (v.Length == 2 && v.All(char.IsLetter))
                return false;
            return true;
        }

        private void Integer(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Integer,
                   (id, v) => new IntegerAttribute { AttributeDefinitionId = id, Value = (long)v });

        private void Float(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Float,
                   (id, v) => new FloatAttribute { AttributeDefinitionId = id, Value = (double)v });

        private void Date(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.IsoDate,
                   (id, v) => new DateAttribute { AttributeDefinitionId = id, Value = (DateTimeOffset)v });

        private void Add(string ns, string key, int definitionId, ValueCoercer.Coercer coercer, AttributeBuilder build, bool multi = false, Func<string, bool>? filter = null)
            => rules[(Normalize(ns), Normalize(key))] = new Rule(definitionId, coercer, build, multi, filter);

        #endregion

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
