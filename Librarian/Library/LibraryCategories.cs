using System;
using System.Collections.Generic;
using System.Linq;
using Librarian.Model.MetadataAttributes;

namespace Librarian.Library
{
    /// <summary>How a category decides which indexed files belong to it.</summary>
    public enum CategoryFilterKind
    {
        /// <summary>All indexed files.</summary>
        None,
        /// <summary>Files whose MIME type starts with one of <see cref="CategoryFilter.MimePrefixes"/>.</summary>
        MimePrefix,
        /// <summary>Files that have a specific attribute.</summary>
        HasAttribute,
        /// <summary>Files that have any attribute in a given vocabulary group.</summary>
        HasGroup,
    }

    public class CategoryFilter
    {
        public CategoryFilterKind Kind { get; init; }
        public string[] MimePrefixes { get; init; } = Array.Empty<string>();
        public int AttributeDefinitionId { get; init; }
        public string? Group { get; init; }

        public static readonly CategoryFilter None = new() { Kind = CategoryFilterKind.None };

        public static CategoryFilter Mime(params string[] prefixes) =>
            new() { Kind = CategoryFilterKind.MimePrefix, MimePrefixes = prefixes };

        public static CategoryFilter HasAttribute(int attributeDefinitionId) =>
            new() { Kind = CategoryFilterKind.HasAttribute, AttributeDefinitionId = attributeDefinitionId };

        public static CategoryFilter HasGroup(string group) =>
            new() { Kind = CategoryFilterKind.HasGroup, Group = group };
    }

    /// <summary>How a drill level produces its virtual folders.</summary>
    public enum DrillKind
    {
        /// <summary>Group by the distinct values of a canonical attribute.</summary>
        Attribute,
        /// <summary>Group by the file's derived media type (see <see cref="MediaType"/>), reconciled
        /// from its MIME signals rather than read from a single attribute.</summary>
        MediaType,
        /// <summary>Group by the year of a date attribute (<see cref="DrillLevel.AttributeDefinitionId"/>).</summary>
        DateYear,
        /// <summary>Group by the month of a date attribute (within an already-selected year).</summary>
        DateMonth,
    }

    /// <summary>One level of a view's ordered drill-down path. Each distinct value (an attribute
    /// value, or a derived media-type label) becomes a virtual folder at this level.</summary>
    public class DrillLevel
    {
        public int AttributeDefinitionId { get; }

        /// <summary>Human label for breadcrumbs / column header (e.g. "Album artist").</summary>
        public string Label { get; }

        public DrillKind Kind { get; }

        public DrillLevel(int attributeDefinitionId, string label, DrillKind kind = DrillKind.Attribute)
        {
            AttributeDefinitionId = attributeDefinitionId;
            Label = label;
            Kind = kind;
        }

        /// <summary>A level that groups files by their derived media type.</summary>
        public static DrillLevel MediaType(string label = "Type") => new(0, label, DrillKind.MediaType);

        /// <summary>A level that groups files by the year of the given date attribute.</summary>
        public static DrillLevel DateYear(int dateDefId, string label = "Year") => new(dateDefId, label, DrillKind.DateYear);

        /// <summary>A level that groups files by the month of the given date attribute.</summary>
        public static DrillLevel DateMonth(int dateDefId, string label = "Month") => new(dateDefId, label, DrillKind.DateMonth);
    }

    /// <summary>
    /// A named facet view within a category — e.g. Music's "By Artist". A view = (ordered drill-down
    /// path) + (leaf sort). Browsing it level by level mirrors browsing a folder tree; the deepest
    /// level lists the matching files. The category supplies the file filter shared by all its views.
    /// </summary>
    public class CategoryView
    {
        public string Key { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public IReadOnlyList<DrillLevel> Path { get; init; } = Array.Empty<DrillLevel>();

        /// <summary>Attribute to sort leaf files by; falls back to natural name order.</summary>
        public int? LeafSortAttributeId { get; init; }
    }

    /// <summary>
    /// A top-level library entry (Music, Video, …). It owns a file <see cref="Filter"/> and a set of
    /// named <see cref="Views"/> ("By Artist", "By Year", …). Browsing the category lists its views
    /// as folders; opening a view drills its path. Computed live from the typed attribute tables
    /// (see <see cref="Librarian.Services.LibraryService"/>).
    /// </summary>
    public class LibraryCategory
    {
        public string Key { get; init; } = null!;
        public string DisplayName { get; init; } = null!;

        /// <summary>File name under <c>~/icons/16/places/</c>.</summary>
        public string Icon { get; init; } = null!;

        public CategoryFilter Filter { get; init; } = CategoryFilter.None;
        public IReadOnlyList<CategoryView> Views { get; init; } = Array.Empty<CategoryView>();

        public CategoryView? FindView(string? key) =>
            Views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The default built-in categories and their facet views, grounded in the actual vocabulary.
    /// User-defined categories (smart views, Phase 6c) will eventually be stored alongside these.
    /// </summary>
    public static class LibraryCategories
    {
        // ---- reusable drill levels --------------------------------------------------------------
        private static readonly DrillLevel AlbumArtist = new(Audio.AlbumArtist, "Album artist");
        private static readonly DrillLevel Album = new(Audio.Album, "Album");
        private static readonly DrillLevel Genre = new(Media.Genre, "Genre");
        private static readonly DrillLevel Year = new(General.Year, "Year");
        private static readonly DrillLevel Series = new(General.Collection, "Series");
        private static readonly DrillLevel Season = new(Media.SeasonNumber, "Season");
        private static readonly DrillLevel Platform = new(Software.Platform, "Platform");
        private static readonly DrillLevel Architecture = new(Software.Architecture, "Architecture");
        private static readonly DrillLevel Author = new(General.WrittenBy, "Author");
        private static readonly DrillLevel Type = DrillLevel.MediaType("Type");
        private static readonly DrillLevel Tag = new(General.Tag, "Tag");
        private static readonly DrillLevel PhotoYear = DrillLevel.DateYear(FileAttributes.DateModified, "Year");
        private static readonly DrillLevel PhotoMonth = DrillLevel.DateMonth(FileAttributes.DateModified, "Month");

        public static readonly IReadOnlyList<LibraryCategory> All = new List<LibraryCategory>
        {
            new()
            {
                Key = "music", DisplayName = "Music", Icon = "music.png",
                Filter = CategoryFilter.Mime("audio/"),
                Views = new[]
                {
                    new CategoryView { Key = "by-artist", DisplayName = "By Artist", Path = new[] { AlbumArtist, Album }, LeafSortAttributeId = Audio.Track },
                    new CategoryView { Key = "by-album",  DisplayName = "By Album",  Path = new[] { Album },            LeafSortAttributeId = Audio.Track },
                    new CategoryView { Key = "by-genre",  DisplayName = "By Genre",  Path = new[] { Genre, AlbumArtist, Album }, LeafSortAttributeId = Audio.Track },
                    new CategoryView { Key = "by-year",   DisplayName = "By Year",   Path = new[] { Year, Album },      LeafSortAttributeId = Audio.Track },
                },
            },
            new()
            {
                Key = "video", DisplayName = "Video", Icon = "movies.png",
                Filter = CategoryFilter.Mime("video/"),
                Views = new[]
                {
                    new CategoryView { Key = "by-genre", DisplayName = "By Genre", Path = new[] { Genre, Year }, LeafSortAttributeId = General.Title },
                    new CategoryView { Key = "by-year",  DisplayName = "By Year",  Path = new[] { Year },        LeafSortAttributeId = General.Title },
                    new CategoryView { Key = "tv-shows", DisplayName = "TV Shows", Path = new[] { Series, Season }, LeafSortAttributeId = Media.EpisodeNumber },
                },
            },
            new()
            {
                Key = "photos", DisplayName = "Photos", Icon = "photos.png",
                Filter = CategoryFilter.Mime("image/"),
                Views = new[]
                {
                    new CategoryView { Key = "all",     DisplayName = "All Photos", Path = Array.Empty<DrillLevel>() },
                    // File modified date stands in as the photo timeline (exif DateRecorded is used
                    // automatically once present); year ▸ month.
                    new CategoryView { Key = "by-date", DisplayName = "By Date",    Path = new[] { PhotoYear, PhotoMonth } },
                    new CategoryView { Key = "by-type", DisplayName = "By Type",    Path = new[] { Type } },
                },
            },
            new()
            {
                Key = "documents", DisplayName = "Documents", Icon = "documents.png",
                Filter = CategoryFilter.Mime(
                    "text/", "application/pdf", "application/epub",
                    "application/msword", "application/rtf",
                    "application/vnd.openxmlformats-officedocument",
                    "application/vnd.oasis.opendocument",
                    // E-books
                    "application/x-mobipocket-ebook", "application/vnd.amazon.ebook",
                    "application/x-fictionbook+xml"),
                Views = new[]
                {
                    new CategoryView { Key = "by-type",   DisplayName = "By Type",   Path = new[] { Type }, LeafSortAttributeId = General.Title },
                    new CategoryView { Key = "by-author", DisplayName = "By Author", Path = new[] { Author },   LeafSortAttributeId = General.Title },
                },
            },
            new()
            {
                Key = "software", DisplayName = "Software", Icon = "software.png",
                Filter = CategoryFilter.HasGroup("Software"),
                Views = new[]
                {
                    new CategoryView { Key = "by-platform",     DisplayName = "By Platform",     Path = new[] { Platform, Architecture }, LeafSortAttributeId = General.Title },
                    new CategoryView { Key = "by-architecture", DisplayName = "By Architecture", Path = new[] { Architecture },           LeafSortAttributeId = General.Title },
                },
            },
            new()
            {
                Key = "tags", DisplayName = "Tags", Icon = "tags.png",
                Filter = CategoryFilter.HasAttribute(General.Tag),
                Views = new[]
                {
                    // Tag is multi-valued: a file with several tags appears under each tag folder.
                    new CategoryView { Key = "by-tag", DisplayName = "By Tag", Path = new[] { Tag } },
                },
            },
            new()
            {
                Key = "all", DisplayName = "All by type", Icon = "all.png",
                Filter = CategoryFilter.None,
                Views = new[]
                {
                    new CategoryView { Key = "by-type", DisplayName = "By Type", Path = new[] { Type } },
                },
            },
        };

        public static LibraryCategory? Find(string? key) =>
            All.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
