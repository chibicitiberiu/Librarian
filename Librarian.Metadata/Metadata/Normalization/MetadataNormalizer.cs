using System.Globalization;
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

        private record Rule(int DefinitionId, ValueCoercer.Coercer Coercer, AttributeBuilder Build, bool Multi = false, Func<string, bool>? Filter = null, double? Min = null, double? Max = null);

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

                // Range validation: reject values outside an attribute's plausible bounds (e.g. a
                // mis-parsed sample rate). The original string is still kept in the raw layer.
                if (IsOutOfRange(coerced, rule.Min, rule.Max))
                {
                    logger?.LogTrace("Value '{value}' for {namespace}:{key} is outside [{min}, {max}]", raw, @namespace, key, rule.Min, rule.Max);
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
            Text("tika", "content-type", General.ContentType, ValueCoercer.MimeType);

            // EXIF (example of a source-specific date transform living next to the mapping).
            // These keys are emitted by both Tika's image parser and the exiftool provider (which
            // augments it); idempotent canonical writes dedupe any overlap by (definition, value).
            Date("exif", "datetimeoriginal", Media.DateRecorded, ValueCoercer.ExifDate);

            RegisterImageRules();
            RegisterAudioRules();
            RegisterMediaRules();
            // ExifTool exposes the same audio tags under vorbis: (FLAC/Ogg) and id3: (MP3) that Tika
            // exposes under xmpDM:/tika: — map them so exiftool actually contributes.
            RegisterAudioTagRules("vorbis", includeGenre: true);
            RegisterAudioTagRules("id3", includeGenre: false); // exiftool -n makes the ID3 genre numeric
            RegisterDocumentRules();
            RegisterSoftwareRules();
            RegisterVideoTagRules();
            RegisterExternalIdRules();
        }

        // Video container tags (Matroska / QuickTime) surfaced by the exiftool provider. The technical
        // atoms stay in the raw layer (we never filter raw); these promote the human-meaningful tags a
        // video actually carries so the Video facets aren't empty for tagged files.
        private void RegisterVideoTagRules()
        {
            Text("matroska", "title", General.Title);
            Text("quicktime", "title", General.Title);
            TextMulti("matroska", "genre", Media.Genre);
            Text("matroska", "comment", General.Comment);
            Text("quicktime", "comment", General.Comment);
            Text("matroska", "description", General.Description);
            Text("matroska", "synopsis", General.Synopsis);
            Text("matroska", "summary", General.Summary);
            Text("matroska", "show", General.Collection);
            Integer("matroska", "season_number", Media.SeasonNumber);
            Integer("matroska", "episode_sort", Media.EpisodeNumber);
        }

        // External-database identifiers — captured wherever a provider exposes them (container tags,
        // ID3/Vorbis, sidecars) so we can later fetch richer metadata from those services. MusicBrainz /
        // AcoustID IDs are mapped under the audio rules; these add the film/TV databases.
        private void RegisterExternalIdRules()
        {
            foreach (var ns in new[] { "tika", "matroska", "id3", "vorbis", "xmpdm", "quicktime" })
            {
                Text(ns, "imdb_id", Media.IMDbID);
                Text(ns, "imdbid", Media.IMDbID);
                Text(ns, "imdb", Media.IMDbID);
                Text(ns, "tmdb_id", Media.TMDbID);
                Text(ns, "tmdbid", Media.TMDbID);
                Text(ns, "themoviedb", Media.TMDbID);
                Text(ns, "tvdb_id", Media.TVDBID);
                Text(ns, "tvdbid", Media.TVDBID);
                Text(ns, "thetvdb", Media.TVDBID);
            }
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

            // Tika reports image dimensions under tiff: (clean integers — note TIFF "ImageLength" is the
            // height) and under "tika:Image Width/Height" as "N pixels" (lenient integer strips the unit).
            // exiftool's file:/exif: copies are mapped above; quicktime: carries a video's frame size.
            Integer("tiff", "imagewidth", Image.Width);
            Integer("tiff", "imagelength", Image.Height);
            Integer("tika", "image width", Image.Width, ValueCoercer.IntegerLoose);
            Integer("tika", "image height", Image.Height, ValueCoercer.IntegerLoose);
            Integer("quicktime", "imagewidth", Image.Width);
            Integer("quicktime", "imageheight", Image.Height);
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

            // ReplayGain (Tika emits these for FLAC/MP3). Values commonly carry a " dB" suffix
            // ("0.00 dB", "-5.81 dB") that a plain float parse would reject — the lenient Number
            // coercer keeps the leading value (dB is already the canonical unit). Peaks are bare floats.
            Float("tika", "replaygain_track_gain", Audio.TrackGain, ValueCoercer.Number);
            Float("tika", "replaygain_track_peak", Audio.TrackPeak, ValueCoercer.Number);
            Float("tika", "replaygain_album_gain", Audio.AlbumGain, ValueCoercer.Number);
            Float("tika", "replaygain_album_peak", Audio.AlbumPeak, ValueCoercer.Number);
            Float("tika", "replaygain_reference_loudness", Audio.ReferenceLoudness, ValueCoercer.Number);

            // Technical stream facts (one raw source each, to avoid duplicate canonical values).
            // Sample rate is unit-aware (handles "44.1 kHz") and range-checked to a plausible band.
            Integer("tika", "samplerate", Audio.SampleRate, UnitCategory.Frequency, min: 8000, max: 192000);
            Integer("xmpdm", "audiosamplerate", Audio.SampleRate, UnitCategory.Frequency, min: 8000, max: 192000);
            Integer("tika", "channels", Audio.Channels);
            Integer("tika", "bits", Audio.BitsPerSample);

            // FLAC technical facts from exiftool's flac: namespace (clean integers); the audio channel
            // *count* only comes from here / meta-cli (Tika reports xmpDM:audioChannelType = "Stereo").
            Integer("flac", "samplerate", Audio.SampleRate, UnitCategory.Frequency, min: 8000, max: 192000);
            Integer("flac", "channels", Audio.Channels);
            Integer("flac", "bitspersample", Audio.BitsPerSample);
        }

        // Media stream facts that frequently arrive with a unit or in a non-numeric form. The
        // unit-aware coercers normalize "320 kbps" / "44.1 kHz" / "2:30.5" to the canonical base
        // (bps / Hz / seconds) so they aren't dropped; range checks reject implausible values. Keys
        // cover both Tika and the exiftool provider (quicktime / composite groups).
        private void RegisterMediaRules()
        {
            Integer("tika", "bitrate", Media.BitRate, UnitCategory.DataRate);
            Integer("composite", "avgbitrate", Media.BitRate, UnitCategory.DataRate);
            Integer("quicktime", "audiobitrate", Media.BitRate, UnitCategory.DataRate);

            Duration("xmpdm", "duration", Media.Duration);
            Duration("tika", "duration", Media.Duration);
            Duration("quicktime", "duration", Media.Duration);
            Duration("composite", "duration", Media.Duration);

            Float("xmpdm", "videoframerate", Video.FrameRate, UnitCategory.FrameRate, min: 1, max: 240);
            Float("quicktime", "videoframerate", Video.FrameRate, UnitCategory.FrameRate, min: 1, max: 240);

            // Codec / compressor (xmpDM audio/video compressor; QuickTime fourcc).
            Text("xmpdm", "audiocompressor", Media.Codec);
            Text("xmpdm", "videocompressor", Media.Codec);
            Text("quicktime", "compressorid", Media.Codec);

            // Cataloguing fields Tika surfaces but didn't map (sort names, grouping, physical media,
            // MusicBrainz album status/type, the original release date).
            Text("tika", "artists", Audio.Artist);
            Text("tika", "artistsort", Audio.ArtistSort);
            Text("tika", "albumartistsort", Audio.AlbumArtistSort);
            Text("tika", "grouping", General.Collection);
            Text("tika", "media", Media.MediaFormat);
            Date("tika", "originaldate", General.DateCreated);
            Text("tika", "musicbrainz_albumstatus", General.ReleaseStatus);
            Text("tika", "musicbrainz_albumtype", General.ReleaseType);
        }

        // ExifTool surfaces FLAC/Vorbis comments under "vorbis:" and ID3 frames under "id3:" — the same
        // concepts Tika reports under xmpDM:/tika:. Mapping them lets exiftool actually contribute;
        // idempotent canonical writes dedupe any overlap with Tika. Vorbis tag names arrive de-underscored
        // (e.g. "albumartistsort", "musicbrainztrackid", "replaygaintrackgain").
        private void RegisterAudioTagRules(string ns, bool includeGenre)
        {
            Text(ns, "title", General.Title);
            Text(ns, "artist", Audio.Artist);
            Text(ns, "artists", Audio.Artist);
            Text(ns, "album", Audio.Album);
            Text(ns, "albumartist", Audio.AlbumArtist);
            Text(ns, "band", Audio.AlbumArtist); // ID3 TPE2 = album artist
            Text(ns, "composer", Audio.Composer);
            Text(ns, "artistsort", Audio.ArtistSort);
            Text(ns, "albumartistsort", Audio.AlbumArtistSort);
            Integer(ns, "track", Audio.Track);
            Integer(ns, "tracknumber", Audio.Track);
            Integer(ns, "tracktotal", Audio.TotalTracks);
            Integer(ns, "totaltracks", Audio.TotalTracks);
            Integer(ns, "discnumber", Media.Disc);
            Integer(ns, "disctotal", Media.TotalDiscs);
            Integer(ns, "totaldiscs", Media.TotalDiscs);
            Integer(ns, "year", General.Year);
            Integer(ns, "originalyear", General.Year);
            Date(ns, "date", General.DateReleased);
            Date(ns, "originaldate", General.DateCreated);
            Text(ns, "label", Media.Label);
            Text(ns, "barcode", General.BarCode);
            Text(ns, "catalognumber", General.CatalogNumber);
            Text(ns, "asin", General.AmazonStandardIdentificationNumberASIN);
            Text(ns, "media", Media.MediaFormat);
            Text(ns, "script", General.Script);
            Text(ns, "releasestatus", General.ReleaseStatus);
            Text(ns, "releasetype", General.ReleaseType);
            Text(ns, "releasecountry", General.ReleaseCountry);
            Text(ns, "organization", General.Organization);
            Text(ns, "grouping", General.Collection);
            Text(ns, "musicbrainzalbumid", Media.MusicBrainzAlbumID);
            Text(ns, "musicbrainzartistid", Media.MusicBrainzArtistID);
            Text(ns, "musicbrainzalbumartistid", Media.MusicBrainzAlbumArtistID);
            Text(ns, "musicbrainztrackid", Media.MusicBrainzTrackID);
            Text(ns, "musicbrainzreleasegroupid", Media.MusicBrainzReleaseGroupID);
            Text(ns, "musicbrainzreleasetrackid", Media.MusicBrainzReleaseTrackID);
            Text(ns, "acoustidid", Audio.AcoustIDID);
            Float(ns, "replaygaintrackgain", Audio.TrackGain, ValueCoercer.Number);
            Float(ns, "replaygaintrackpeak", Audio.TrackPeak, ValueCoercer.Number);
            Float(ns, "replaygainalbumgain", Audio.AlbumGain, ValueCoercer.Number);
            Float(ns, "replaygainalbumpeak", Audio.AlbumPeak, ValueCoercer.Number);
            if (includeGenre)
                TextMulti(ns, "genre", Media.Genre);
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

            // exiftool's exe: namespace (PE VERSIONINFO + header) — richer than Tika's machine: parser.
            Text("exe", "productname", General.Product);
            Text("exe", "companyname", General.Publisher);
            Text("exe", "filedescription", General.Description);
            Text("exe", "legalcopyright", General.Copyright);
            Text("exe", "fileversion", Package.Version);
            Text("exe", "productversion", Package.Version);
            // exiftool -n reports MachineType as a numeric PE code; map it to a friendly arch name.
            Text("exe", "machinetype", Software.Architecture, ValueCoercer.PeMachineType);
        }

        #endregion

        #region Rule helpers

        private void Text(string ns, string key, int definitionId)
            => Add(ns, key, definitionId, ValueCoercer.Text,
                   (id, v) => new TextAttribute { AttributeDefinitionId = id, Value = (string)v });

        /// <summary>A text field with a custom value transform (e.g. stripping MIME parameters).</summary>
        private void Text(string ns, string key, int definitionId, ValueCoercer.Coercer coercer)
            => Add(ns, key, definitionId, coercer,
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

        private void Integer(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null, double? min = null, double? max = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Integer,
                   (id, v) => new IntegerAttribute { AttributeDefinitionId = id, Value = (long)v }, min: min, max: max);

        /// <summary>An integer field whose raw value may carry a unit (e.g. "320 kbps"); the value is
        /// converted to <paramref name="unit"/>'s canonical base before storage.</summary>
        private void Integer(string ns, string key, int definitionId, UnitCategory unit, double? min = null, double? max = null)
            => Integer(ns, key, definitionId, ValueCoercer.IntegerIn(unit), min, max);

        private void Float(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null, double? min = null, double? max = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.Float,
                   (id, v) => new FloatAttribute { AttributeDefinitionId = id, Value = (double)v }, min: min, max: max);

        /// <summary>A float field whose raw value may carry a unit (see the integer overload).</summary>
        private void Float(string ns, string key, int definitionId, UnitCategory unit, double? min = null, double? max = null)
            => Float(ns, key, definitionId, ValueCoercer.FloatIn(unit), min, max);

        private void Date(string ns, string key, int definitionId, ValueCoercer.Coercer? coercer = null)
            => Add(ns, key, definitionId, coercer ?? ValueCoercer.IsoDate,
                   (id, v) => new DateAttribute { AttributeDefinitionId = id, Value = (DateTimeOffset)v }, filter: IsRealDate);

        // Reject placeholder dates that aren't real: exiftool's all-zero date, and the Unix (1970) /
        // QuickTime (1904) epoch zeros containers emit when no date was set — they otherwise pollute
        // canonical Date fields. The raw value is untouched (kept in the raw layer); only promotion is skipped.
        private static bool IsRealDate(string raw)
        {
            string v = raw.TrimStart();
            return !(v.StartsWith("0000")
                  || v.StartsWith("1904-01-01") || v.StartsWith("1904:01:01")
                  || v.StartsWith("1970-01-01") || v.StartsWith("1970:01:01"));
        }

        /// <summary>A duration (TimeSpan) field stored as total seconds (a <see cref="FloatAttribute"/>,
        /// matching how the factory persists TimeSpan); accepts seconds, HH:MM:SS, M:SS.ms, etc.</summary>
        private void Duration(string ns, string key, int definitionId, double? min = null, double? max = null)
            => Add(ns, key, definitionId, ValueCoercer.Duration,
                   (id, v) => new FloatAttribute { AttributeDefinitionId = id, Value = (double)v }, min: min, max: max);

        private void Add(string ns, string key, int definitionId, ValueCoercer.Coercer coercer, AttributeBuilder build, bool multi = false, Func<string, bool>? filter = null, double? min = null, double? max = null)
            => rules[(Normalize(ns), Normalize(key))] = new Rule(definitionId, coercer, build, multi, filter, min, max);

        private static bool IsOutOfRange(object value, double? min, double? max)
        {
            if (min is null && max is null)
                return false;
            double number;
            try { number = Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return false; }
            return (min is not null && number < min) || (max is not null && number > max);
        }

        #endregion

        private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    }
}
