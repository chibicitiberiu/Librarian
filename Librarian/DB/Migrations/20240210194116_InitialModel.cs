using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class InitialModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttributeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Group = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndexedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Exists = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsUpdating = table.Column<bool>(type: "boolean", nullable: false),
                    IndexLastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Alias = table.Column<string>(type: "text", nullable: false),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeAliases_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IndexedFileContents",
                columns: table => new
                {
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ContentSearch = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedFileContents", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_IndexedFileContents_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    InternalId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubResources_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Editable = table.Column<bool>(type: "boolean", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderAttributeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobAttributes_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlobAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlobAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DateAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Editable = table.Column<bool>(type: "boolean", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderAttributeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateAttributes_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DateAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DateAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FloatAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Editable = table.Column<bool>(type: "boolean", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderAttributeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloatAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloatAttributes_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FloatAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FloatAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IntegerAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Editable = table.Column<bool>(type: "boolean", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderAttributeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegerAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegerAttributes_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegerAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegerAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TextAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueSearch = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    AttributeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Editable = table.Column<bool>(type: "boolean", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ProviderAttributeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextAttributes_AttributeDefinitions_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "AttributeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AttributeAliases",
                columns: new[] { "Id", "Alias", "AttributeDefinitionId", "Role" },
                values: new object[] { 105, "filename", null, 1 });

            migrationBuilder.InsertData(
                table: "AttributeDefinitions",
                columns: new[] { "Id", "Description", "Group", "IsReadOnly", "Name", "Type", "Unit" },
                values: new object[,]
                {
                    { 1, "AcoustID identifier", "Audio", false, "AcoustID ID", 0, "" },
                    { 2, "", "Audio", false, "Album", 0, "" },
                    { 3, "", "Audio", false, "Album artist", 0, "" },
                    { 4, "", "Audio", false, "Album artist (sort)", 0, "" },
                    { 5, "The ideal listening gain for an entire album", "Audio", false, "Album gain", 4, "" },
                    { 6, "Peak album amplitude, used to predict whether the required replay gain adjustment will cause clipping during playback", "Audio", false, "Album peak", 4, "dB" },
                    { 7, "", "Audio", false, "Artist", 0, "dB" },
                    { 8, "", "Audio", false, "Artist (sort)", 0, "" },
                    { 9, "", "Audio", false, "Beats per minute", 3, "" },
                    { 10, "", "Audio", false, "Bits per sample", 3, "" },
                    { 11, "", "Audio", false, "Channels", 3, "" },
                    { 12, "", "Audio", false, "Composer", 0, "" },
                    { 13, "", "Audio", false, "Engineer", 0, "" },
                    { 14, "", "Audio", false, "Initial key", 0, "bpm" },
                    { 15, "", "Audio", false, "Lyrics", 1, "bps" },
                    { 16, "", "Audio", false, "Original album", 0, "" },
                    { 17, "", "Audio", false, "Reference loudness", 4, "" },
                    { 18, "", "Audio", false, "Sample rate", 3, "" },
                    { 19, "", "Audio", false, "Total tracks", 3, "" },
                    { 20, "", "Audio", false, "Track ", 3, "" },
                    { 21, "", "Audio", false, "Track artist", 0, "" },
                    { 22, "", "Audio", false, "Track gain", 4, "" },
                    { 23, "", "Audio", false, "Track peak", 4, "" },
                    { 24, "Date and time when the file was created on disk.", "File attributes", false, "Date created", 5, "" },
                    { 25, "Date and time when the file was last modified.", "File attributes", false, "Date modified", 5, "" },
                    { 26, "The file extension.", "File attributes", false, "File extension", 0, "" },
                    { 27, "The file name as it appears on disk.", "File attributes", false, "File name", 0, "" },
                    { 28, "File type as detected by the 'file' command.", "File attributes", false, "File type", 0, "" },
                    { 29, "The full file system path to the file.", "File attributes", false, "Full path", 0, "" },
                    { 30, "Number of items (files or folders) in the directory.", "File attributes", false, "Item count", 3, "" },
                    { 31, "The detected mime type of the file.", "File attributes", false, "Mime type", 0, "" },
                    { 32, "The size of the file in bytes.", "File attributes", false, "Size", 3, "" },
                    { 33, "Product identifier used by the Amazon store", "General", false, "Amazon Standard Identification Number (ASIN)", 0, "" },
                    { 34, "", "General", false, "Bar code", 0, "" },
                    { 35, "", "General", false, "Catalog number", 0, "" },
                    { 36, "", "General", false, "Category", 0, "" },
                    { 37, "", "General", false, "Collection", 0, "" },
                    { 38, "", "General", false, "Comment", 1, "" },
                    { 39, "", "General", false, "Compilation", 0, "" },
                    { 40, "", "General", false, "Content rating", 0, "" },
                    { 41, "", "General", false, "Content type", 0, "" },
                    { 42, "", "General", false, "Copyright", 1, "" },
                    { 43, "", "General", false, "Credits", 1, "" },
                    { 44, "Date when this file was created.", "General", false, "Date created", 5, "" },
                    { 45, "Date when this file was released.", "General", false, "Date released", 5, "" },
                    { 46, "Long description of the file, can be formatted with MarkDown.", "General", false, "Description", 2, "" },
                    { 47, "", "General", false, "Director", 0, "" },
                    { 48, "Name that will be displayed in the metadata file browser.", "General", false, "Display name", 0, "" },
                    { 49, "", "General", false, "Encoded by", 0, "" },
                    { 50, "", "General", false, "Encoder", 0, "" },
                    { 51, "", "General", false, "Encoder settings", 0, "" },
                    { 52, "", "General", false, "Id", 0, "" },
                    { 53, "", "General", false, "Index", 3, "" },
                    { 54, "", "General", false, "Language", 0, "" },
                    { 55, "", "General", false, "Location", 0, "" },
                    { 56, "", "General", false, "Minor version", 0, "" },
                    { 57, "", "General", false, "Organization", 0, "" },
                    { 58, "", "General", false, "Product", 0, "" },
                    { 59, "", "General", false, "Publisher", 0, "" },
                    { 60, "", "General", false, "Release country", 0, "" },
                    { 61, "", "General", false, "Release notes", 2, "" },
                    { 62, "", "General", false, "Release status", 0, "" },
                    { 63, "", "General", false, "Release type", 0, "" },
                    { 64, "", "General", false, "Script", 0, "" },
                    { 65, "", "General", false, "Size", 3, "" },
                    { 66, "", "General", false, "Source", 0, "" },
                    { 67, "", "General", false, "Source URL", 0, "" },
                    { 68, "", "General", false, "Subcategory", 0, "" },
                    { 69, "", "General", false, "Subtitle", 0, "" },
                    { 70, "", "General", false, "Summary", 1, "" },
                    { 71, "", "General", false, "Synopsis", 1, "" },
                    { 72, "", "General", false, "Tag", 0, "" },
                    { 73, "", "General", false, "Title", 0, "" },
                    { 74, "", "General", false, "Unique file identifier (UFID)", 0, "" },
                    { 75, "", "General", false, "Uploader", 0, "" },
                    { 76, "", "General", false, "Written by", 0, "" },
                    { 77, "", "General", false, "Year", 3, "" },
                    { 78, "", "General", false, "Year created", 3, "" },
                    { 79, "Ratio obtained by dividing the width by the height.", "Image", false, "Aspect ratio", 4, "" },
                    { 80, "Image height in pixels.", "Image", false, "Height", 3, "" },
                    { 81, "Total number of pixels in this image.", "Image", false, "Pixels", 3, "" },
                    { 82, "Image width in pixels.", "Image", false, "Width", 3, "" },
                    { 83, "", "Media", false, "Actor", 0, "dB" },
                    { 84, "", "Media", false, "Bit rate", 3, "" },
                    { 85, "", "Media", false, "Codec", 0, "" },
                    { 86, "", "Media", false, "Date recorded", 5, "" },
                    { 87, "", "Media", false, "Disc", 3, "" },
                    { 88, "Media duration.", "Media", false, "Duration", 6, "" },
                    { 89, "", "Media", false, "End time", 6, "" },
                    { 90, "", "Media", false, "Episode ID", 0, "" },
                    { 91, "", "Media", false, "Episode number", 3, "" },
                    { 92, "", "Media", false, "Genre", 0, "" },
                    { 93, "", "Media", false, "Label", 0, "" },
                    { 94, "", "Media", false, "Media format", 0, "" },
                    { 95, "", "Media", false, "MusicBrainz album artist ID", 0, "" },
                    { 96, "", "Media", false, "MusicBrainz album ID", 0, "" },
                    { 97, "", "Media", false, "MusicBrainz artist ID", 0, "" },
                    { 98, "", "Media", false, "MusicBrainz release group ID", 0, "" },
                    { 99, "", "Media", false, "MusicBrainz release track ID", 0, "" },
                    { 100, "", "Media", false, "MusicBrainz track ID", 0, "" },
                    { 101, "", "Media", false, "Narrated by", 0, "" },
                    { 102, "", "Media", false, "Producer", 0, "" },
                    { 103, "", "Media", false, "Screenplay by", 0, "" },
                    { 104, "", "Media", false, "Season number", 3, "" },
                    { 105, "", "Media", false, "Start time", 6, "" },
                    { 106, "", "Media", false, "Stream type", 0, "" },
                    { 107, "", "Media", false, "Studio", 0, "" },
                    { 108, "", "Media", false, "Total discs", 3, "" },
                    { 109, "Version of this package.", "Package", false, "Version", 0, "" },
                    { 110, "System architecture for which this software was compiled", "Software", false, "Architecture", 0, "dB" },
                    { 111, "", "Software", false, "End of life date", 5, "dB" },
                    { 112, "", "Software", false, "Installation instructions", 2, "" },
                    { 113, "", "Software", false, "Minimum CPU", 0, "" },
                    { 114, "", "Software", false, "Minimum disk space", 3, "" },
                    { 115, "", "Software", false, "Minimum RAM", 3, "" },
                    { 116, "", "Software", false, "Platform", 0, "" },
                    { 117, "", "Software", false, "Serial key", 0, "" },
                    { 118, "", "Software", false, "User interface", 0, "" },
                    { 119, "", "Video", false, "Frame rate", 4, "" },
                    { 120, "", "Video", false, "Frames", 3, "" }
                });

            migrationBuilder.InsertData(
                table: "AttributeAliases",
                columns: new[] { "Id", "Alias", "AttributeDefinitionId", "Role" },
                values: new object[,]
                {
                    { 1, "acoustid_id", 1, 0 },
                    { 2, "album", 2, 0 },
                    { 3, "album_artist", 3, 0 },
                    { 4, "albumartistsort", 4, 0 },
                    { 5, "replaygain_album_gain", 5, 0 },
                    { 6, "replaygain_album_peak", 6, 0 },
                    { 7, "artist", 7, 0 },
                    { 8, "artists", 7, 0 },
                    { 9, "artistsort", 8, 0 },
                    { 10, "tbp", 9, 0 },
                    { 11, "composer", 12, 0 },
                    { 12, "tcm", 12, 0 },
                    { 13, "ieng", 13, 0 },
                    { 14, "tke", 14, 0 },
                    { 15, "toal", 16, 0 },
                    { 16, "replaygain_reference_loudness", 17, 0 },
                    { 17, "tracktotal", 19, 0 },
                    { 18, "track", 20, 0 },
                    { 19, "musicmatch_trackartist", 21, 0 },
                    { 20, "replaygain_track_gain", 22, 0 },
                    { 21, "replaygain_track_peak", 23, 0 },
                    { 22, "mimetype", 31, 0 },
                    { 23, "asin", 33, 0 },
                    { 24, "barcode", 34, 0 },
                    { 25, "upc", 34, 0 },
                    { 26, "catalognumber", 35, 0 },
                    { 27, "grouping", 37, 0 },
                    { 28, "comment", 38, 0 },
                    { 29, "compilation", 39, 0 },
                    { 30, "law_rating", 40, 0 },
                    { 31, "rating", 40, 0 },
                    { 32, "content_type", 41, 0 },
                    { 33, "copyright", 42, 0 },
                    { 34, "credits", 43, 0 },
                    { 35, "creation_time", 44, 0 },
                    { 36, "date", 44, 0 },
                    { 37, "originaldate", 44, 0 },
                    { 38, "date_release", 45, 0 },
                    { 39, "date_released", 45, 0 },
                    { 40, "tdr", 45, 0 },
                    { 41, "description", 46, 0 },
                    { 42, "tds", 46, 0 },
                    { 43, "director", 47, 0 },
                    { 44, "encoded_by", 49, 0 },
                    { 45, "encoded-by", 49, 0 },
                    { 46, "encodedby", 49, 0 },
                    { 47, "encoder", 50, 0 },
                    { 48, "software", 50, 0 },
                    { 49, "encodersettings", 51, 0 },
                    { 50, "tss", 51, 0 },
                    { 51, "language", 54, 0 },
                    { 52, "location", 55, 0 },
                    { 53, "location-eng", 55, 0 },
                    { 54, "minor_version", 56, 0 },
                    { 55, "organization", 57, 0 },
                    { 56, "product", 58, 0 },
                    { 57, "publisher", 59, 0 },
                    { 58, "releasecountry", 60, 0 },
                    { 59, "releasestatus", 62, 0 },
                    { 60, "releasetype", 63, 0 },
                    { 61, "script", 64, 0 },
                    { 62, "tsiz", 65, 0 },
                    { 63, "isrc", 66, 0 },
                    { 64, "tid", 66, 0 },
                    { 65, "woas", 67, 0 },
                    { 66, "subtitle", 69, 0 },
                    { 67, "tit3", 69, 0 },
                    { 68, "tt3", 69, 0 },
                    { 69, "summary", 70, 0 },
                    { 70, "synopsis", 71, 0 },
                    { 71, "title", 73, 0 },
                    { 72, "ufid", 74, 0 },
                    { 73, "uploader", 75, 0 },
                    { 74, "written_by", 76, 0 },
                    { 75, "tyer", 77, 0 },
                    { 76, "year", 77, 0 },
                    { 77, "originalyear", 78, 0 },
                    { 78, "actor", 83, 0 },
                    { 79, "bps", 84, 0 },
                    { 80, "bps-eng", 84, 0 },
                    { 81, "date_recorded", 86, 0 },
                    { 82, "disc", 87, 0 },
                    { 83, "duration", 88, 0 },
                    { 84, "duration-eng", 88, 0 },
                    { 85, "tlen", 88, 0 },
                    { 86, "episodeid", 90, 0 },
                    { 87, "episodenumber", 91, 0 },
                    { 88, "episode_sort", 91, 0 },
                    { 89, "genre", 92, 0 },
                    { 90, "label", 93, 0 },
                    { 91, "media", 94, 0 },
                    { 92, "musicbrainz_albumartistid", 95, 0 },
                    { 93, "musicbrainz_albumid", 96, 0 },
                    { 94, "musicbrainz_artistid", 97, 0 },
                    { 95, "musicbrainz_releasegroupid", 98, 0 },
                    { 96, "musicbrainz_releasetrackid", 99, 0 },
                    { 97, "musicbrainz_trackid", 100, 0 },
                    { 98, "narratedby", 101, 0 },
                    { 99, "producer", 102, 0 },
                    { 100, "screenplay_by", 103, 0 },
                    { 101, "season_number", 104, 0 },
                    { 102, "seasonnumber", 104, 0 },
                    { 103, "production_studio", 107, 0 },
                    { 104, "disctotal", 108, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttributeAliases_Alias",
                table: "AttributeAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeAliases_AttributeDefinitionId",
                table: "AttributeAliases",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_Group_Name",
                table: "AttributeDefinitions",
                columns: new[] { "Group", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobAttributes_AttributeDefinitionId",
                table: "BlobAttributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobAttributes_FileId",
                table: "BlobAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobAttributes_SubResourceId",
                table: "BlobAttributes",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DateAttributes_AttributeDefinitionId",
                table: "DateAttributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DateAttributes_FileId",
                table: "DateAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_DateAttributes_SubResourceId",
                table: "DateAttributes",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatAttributes_AttributeDefinitionId",
                table: "FloatAttributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatAttributes_FileId",
                table: "FloatAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatAttributes_SubResourceId",
                table: "FloatAttributes",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Path",
                table: "IndexedFiles",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegerAttributes_AttributeDefinitionId",
                table: "IntegerAttributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegerAttributes_FileId",
                table: "IntegerAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegerAttributes_SubResourceId",
                table: "IntegerAttributes",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SubResources_FileId",
                table: "SubResources",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TextAttributes_AttributeDefinitionId",
                table: "TextAttributes",
                column: "AttributeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TextAttributes_FileId",
                table: "TextAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TextAttributes_SubResourceId",
                table: "TextAttributes",
                column: "SubResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttributeAliases");

            migrationBuilder.DropTable(
                name: "BlobAttributes");

            migrationBuilder.DropTable(
                name: "DateAttributes");

            migrationBuilder.DropTable(
                name: "FloatAttributes");

            migrationBuilder.DropTable(
                name: "IndexedFileContents");

            migrationBuilder.DropTable(
                name: "IntegerAttributes");

            migrationBuilder.DropTable(
                name: "TextAttributes");

            migrationBuilder.DropTable(
                name: "AttributeDefinitions");

            migrationBuilder.DropTable(
                name: "SubResources");

            migrationBuilder.DropTable(
                name: "IndexedFiles");
        }
    }
}
