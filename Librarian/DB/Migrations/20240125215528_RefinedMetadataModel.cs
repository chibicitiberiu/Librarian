using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class RefinedMetadataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlobMetadata_MetadataAttributes_AttributeId",
                table: "BlobMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_DateMetadata_MetadataAttributes_AttributeId",
                table: "DateMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_FloatMetadata_MetadataAttributes_AttributeId",
                table: "FloatMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegerMetadata_MetadataAttributes_AttributeId",
                table: "IntegerMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMetadata_MetadataAttributes_AttributeId",
                table: "TextMetadata");

            migrationBuilder.RenameColumn(
                name: "AttributeId",
                table: "TextMetadata",
                newName: "AttributeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_TextMetadata_AttributeId",
                table: "TextMetadata",
                newName: "IX_TextMetadata_AttributeDefinitionId");

            migrationBuilder.RenameColumn(
                name: "Grouping",
                table: "MetadataAttributes",
                newName: "Group");

            migrationBuilder.RenameColumn(
                name: "AttributeId",
                table: "IntegerMetadata",
                newName: "AttributeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_IntegerMetadata_AttributeId",
                table: "IntegerMetadata",
                newName: "IX_IntegerMetadata_AttributeDefinitionId");

            migrationBuilder.RenameColumn(
                name: "AttributeId",
                table: "FloatMetadata",
                newName: "AttributeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_FloatMetadata_AttributeId",
                table: "FloatMetadata",
                newName: "IX_FloatMetadata_AttributeDefinitionId");

            migrationBuilder.RenameColumn(
                name: "AttributeId",
                table: "DateMetadata",
                newName: "AttributeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_DateMetadata_AttributeId",
                table: "DateMetadata",
                newName: "IX_DateMetadata_AttributeDefinitionId");

            migrationBuilder.RenameColumn(
                name: "AttributeId",
                table: "BlobMetadata",
                newName: "AttributeDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_BlobMetadata_AttributeId",
                table: "BlobMetadata",
                newName: "IX_BlobMetadata_AttributeDefinitionId");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "TextMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubResourceId",
                table: "TextMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MetadataAttributes",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "IntegerMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubResourceId",
                table: "IntegerMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "FloatMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubResourceId",
                table: "FloatMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "DateMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubResourceId",
                table: "DateMetadata",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderId",
                table: "BlobMetadata",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubResourceId",
                table: "BlobMetadata",
                type: "integer",
                nullable: true);

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
                        name: "FK_AttributeAliases_MetadataAttributes_AttributeDefinitionId",
                        column: x => x.AttributeDefinitionId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id");
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

            migrationBuilder.InsertData(
                table: "AttributeAliases",
                columns: new[] { "Id", "Alias", "AttributeDefinitionId", "Role" },
                values: new object[] { 105, "filename", null, 1 });

            migrationBuilder.InsertData(
                table: "MetadataAttributes",
                columns: new[] { "Id", "Description", "Group", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "AcoustID identifier", "Audio", "AcoustID ID", 0 },
                    { 2, "", "Audio", "Album", 0 },
                    { 3, "", "Audio", "Album artist", 0 },
                    { 4, "", "Audio", "Album artist (sort)", 0 },
                    { 5, "The ideal listening gain for an entire album", "Audio", "Album gain", 4 },
                    { 6, "Peak album amplitude, used to predict whether the required replay gain adjustment will cause clipping during playback", "Audio", "Album peak", 4 },
                    { 7, "", "Audio", "Artist", 0 },
                    { 8, "", "Audio", "Artist (sort)", 0 },
                    { 9, "", "Audio", "Beats per minute", 3 },
                    { 10, "", "Audio", "Bits per sample", 3 },
                    { 11, "", "Audio", "Channels", 3 },
                    { 12, "", "Audio", "Composer", 0 },
                    { 13, "", "Audio", "Engineer", 0 },
                    { 14, "", "Audio", "Initial key", 0 },
                    { 15, "", "Audio", "Lyrics", 1 },
                    { 16, "", "Audio", "Original album", 0 },
                    { 17, "", "Audio", "Reference loudness", 4 },
                    { 18, "", "Audio", "Sample rate", 3 },
                    { 19, "", "Audio", "Total tracks", 3 },
                    { 20, "", "Audio", "Track ", 3 },
                    { 21, "", "Audio", "Track artist", 0 },
                    { 22, "", "Audio", "Track gain", 4 },
                    { 23, "", "Audio", "Track peak", 4 },
                    { 24, "Date and time when the file was created on disk.", "File attributes", "Date created", 5 },
                    { 25, "Date and time when the file was last modified.", "File attributes", "Date modified", 5 },
                    { 26, "The file extension.", "File attributes", "File extension", 0 },
                    { 27, "The file name as it appears on disk.", "File attributes", "File name", 0 },
                    { 28, "File type as detected by the 'file' command.", "File attributes", "File type", 0 },
                    { 29, "The full file system path to the file.", "File attributes", "Full path", 0 },
                    { 30, "Number of items (files or folders) in the directory.", "File attributes", "Item count", 3 },
                    { 31, "The detected mime type of the file.", "File attributes", "Mime type", 0 },
                    { 32, "The size of the file in bytes.", "File attributes", "Size", 3 },
                    { 33, "Product identifier used by the Amazon store", "General", "Amazon Standard Identification Number (ASIN)", 0 },
                    { 34, "", "General", "Bar code", 0 },
                    { 35, "", "General", "Catalog number", 0 },
                    { 36, "", "General", "Category", 0 },
                    { 37, "", "General", "Collection", 0 },
                    { 38, "", "General", "Comment", 1 },
                    { 39, "", "General", "Compilation", 0 },
                    { 40, "", "General", "Content rating", 0 },
                    { 41, "", "General", "Content type", 0 },
                    { 42, "", "General", "Copyright", 1 },
                    { 43, "", "General", "Credits", 1 },
                    { 44, "Date when this file was created.", "General", "Date created", 5 },
                    { 45, "Date when this file was released.", "General", "Date released", 5 },
                    { 46, "Long description of the file, can be formatted with MarkDown.", "General", "Description", 2 },
                    { 47, "", "General", "Director", 0 },
                    { 48, "Name that will be displayed in the metadata file browser.", "General", "Display name", 0 },
                    { 49, "", "General", "Encoded by", 0 },
                    { 50, "", "General", "Encoder", 0 },
                    { 51, "", "General", "Encoder settings", 0 },
                    { 52, "", "General", "Id", 0 },
                    { 53, "", "General", "Index", 3 },
                    { 54, "", "General", "Language", 0 },
                    { 55, "", "General", "Location", 0 },
                    { 56, "", "General", "Minor version", 0 },
                    { 57, "", "General", "Organization", 0 },
                    { 58, "", "General", "Product", 0 },
                    { 59, "", "General", "Publisher", 0 },
                    { 60, "", "General", "Release country", 0 },
                    { 61, "", "General", "Release notes", 2 },
                    { 62, "", "General", "Release status", 0 },
                    { 63, "", "General", "Release type", 0 },
                    { 64, "", "General", "Script", 0 },
                    { 65, "", "General", "Size", 3 },
                    { 66, "", "General", "Source", 0 },
                    { 67, "", "General", "Source URL", 0 },
                    { 68, "", "General", "Subcategory", 0 },
                    { 69, "", "General", "Subtitle", 0 },
                    { 70, "", "General", "Summary", 1 },
                    { 71, "", "General", "Synopsis", 1 },
                    { 72, "", "General", "Tag", 0 },
                    { 73, "", "General", "Title", 0 },
                    { 74, "", "General", "Unique file identifier (UFID)", 0 },
                    { 75, "", "General", "Uploader", 0 },
                    { 76, "", "General", "Written by", 0 },
                    { 77, "", "General", "Year", 3 },
                    { 78, "", "General", "Year created", 3 },
                    { 79, "Ratio obtained by dividing the width by the height.", "Image", "Aspect ratio", 4 },
                    { 80, "Image height in pixels.", "Image", "Height", 3 },
                    { 81, "Total number of pixels in this image.", "Image", "Pixels", 3 },
                    { 82, "Image width in pixels.", "Image", "Width", 3 },
                    { 83, "", "Media", "Actor", 0 },
                    { 84, "", "Media", "Bit rate", 3 },
                    { 85, "", "Media", "Codec", 0 },
                    { 86, "", "Media", "Date recorded", 5 },
                    { 87, "", "Media", "Disc", 3 },
                    { 88, "Media duration.", "Media", "Duration", 6 },
                    { 89, "", "Media", "End time", 6 },
                    { 90, "", "Media", "Episode ID", 0 },
                    { 91, "", "Media", "Episode number", 3 },
                    { 92, "", "Media", "Genre", 0 },
                    { 93, "", "Media", "Label", 0 },
                    { 94, "", "Media", "Media format", 0 },
                    { 95, "", "Media", "MusicBrainz album artist ID", 0 },
                    { 96, "", "Media", "MusicBrainz album ID", 0 },
                    { 97, "", "Media", "MusicBrainz artist ID", 0 },
                    { 98, "", "Media", "MusicBrainz release group ID", 0 },
                    { 99, "", "Media", "MusicBrainz release track ID", 0 },
                    { 100, "", "Media", "MusicBrainz track ID", 0 },
                    { 101, "", "Media", "Narrated by", 0 },
                    { 102, "", "Media", "Producer", 0 },
                    { 103, "", "Media", "Screenplay by", 0 },
                    { 104, "", "Media", "Season number", 3 },
                    { 105, "", "Media", "Start time", 6 },
                    { 106, "", "Media", "Stream type", 0 },
                    { 107, "", "Media", "Studio", 0 },
                    { 108, "", "Media", "Total discs", 3 },
                    { 109, "Version of this package.", "Package", "Version", 0 },
                    { 110, "System architecture for which this software was compiled", "Software", "Architecture", 0 },
                    { 111, "", "Software", "End of life date", 5 },
                    { 112, "", "Software", "Installation instructions", 2 },
                    { 113, "", "Software", "Minimum CPU", 0 },
                    { 114, "", "Software", "Minimum disk space", 3 },
                    { 115, "", "Software", "Minimum RAM", 3 },
                    { 116, "", "Software", "Platform", 0 },
                    { 117, "", "Software", "Serial key", 0 },
                    { 118, "", "Software", "User interface", 0 },
                    { 119, "", "Video", "Frame rate", 4 },
                    { 120, "", "Video", "Frames", 3 }
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
                name: "IX_TextMetadata_SubResourceId",
                table: "TextMetadata",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataAttributes_Group_Name",
                table: "MetadataAttributes",
                columns: new[] { "Group", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegerMetadata_SubResourceId",
                table: "IntegerMetadata",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatMetadata_SubResourceId",
                table: "FloatMetadata",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DateMetadata_SubResourceId",
                table: "DateMetadata",
                column: "SubResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobMetadata_SubResourceId",
                table: "BlobMetadata",
                column: "SubResourceId");

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
                name: "IX_SubResources_FileId",
                table: "SubResources",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlobMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "BlobMetadata",
                column: "AttributeDefinitionId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlobMetadata_SubResources_SubResourceId",
                table: "BlobMetadata",
                column: "SubResourceId",
                principalTable: "SubResources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DateMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "DateMetadata",
                column: "AttributeDefinitionId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DateMetadata_SubResources_SubResourceId",
                table: "DateMetadata",
                column: "SubResourceId",
                principalTable: "SubResources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FloatMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "FloatMetadata",
                column: "AttributeDefinitionId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloatMetadata_SubResources_SubResourceId",
                table: "FloatMetadata",
                column: "SubResourceId",
                principalTable: "SubResources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IntegerMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "IntegerMetadata",
                column: "AttributeDefinitionId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegerMetadata_SubResources_SubResourceId",
                table: "IntegerMetadata",
                column: "SubResourceId",
                principalTable: "SubResources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TextMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "TextMetadata",
                column: "AttributeDefinitionId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMetadata_SubResources_SubResourceId",
                table: "TextMetadata",
                column: "SubResourceId",
                principalTable: "SubResources",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlobMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "BlobMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_BlobMetadata_SubResources_SubResourceId",
                table: "BlobMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_DateMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "DateMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_DateMetadata_SubResources_SubResourceId",
                table: "DateMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_FloatMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "FloatMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_FloatMetadata_SubResources_SubResourceId",
                table: "FloatMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegerMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "IntegerMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegerMetadata_SubResources_SubResourceId",
                table: "IntegerMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMetadata_MetadataAttributes_AttributeDefinitionId",
                table: "TextMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMetadata_SubResources_SubResourceId",
                table: "TextMetadata");

            migrationBuilder.DropTable(
                name: "AttributeAliases");

            migrationBuilder.DropTable(
                name: "SubResources");

            migrationBuilder.DropIndex(
                name: "IX_TextMetadata_SubResourceId",
                table: "TextMetadata");

            migrationBuilder.DropIndex(
                name: "IX_MetadataAttributes_Group_Name",
                table: "MetadataAttributes");

            migrationBuilder.DropIndex(
                name: "IX_IntegerMetadata_SubResourceId",
                table: "IntegerMetadata");

            migrationBuilder.DropIndex(
                name: "IX_FloatMetadata_SubResourceId",
                table: "FloatMetadata");

            migrationBuilder.DropIndex(
                name: "IX_DateMetadata_SubResourceId",
                table: "DateMetadata");

            migrationBuilder.DropIndex(
                name: "IX_BlobMetadata_SubResourceId",
                table: "BlobMetadata");

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "MetadataAttributes",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DropColumn(
                name: "SubResourceId",
                table: "TextMetadata");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MetadataAttributes");

            migrationBuilder.DropColumn(
                name: "SubResourceId",
                table: "IntegerMetadata");

            migrationBuilder.DropColumn(
                name: "SubResourceId",
                table: "FloatMetadata");

            migrationBuilder.DropColumn(
                name: "SubResourceId",
                table: "DateMetadata");

            migrationBuilder.DropColumn(
                name: "SubResourceId",
                table: "BlobMetadata");

            migrationBuilder.RenameColumn(
                name: "AttributeDefinitionId",
                table: "TextMetadata",
                newName: "AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_TextMetadata_AttributeDefinitionId",
                table: "TextMetadata",
                newName: "IX_TextMetadata_AttributeId");

            migrationBuilder.RenameColumn(
                name: "Group",
                table: "MetadataAttributes",
                newName: "Grouping");

            migrationBuilder.RenameColumn(
                name: "AttributeDefinitionId",
                table: "IntegerMetadata",
                newName: "AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_IntegerMetadata_AttributeDefinitionId",
                table: "IntegerMetadata",
                newName: "IX_IntegerMetadata_AttributeId");

            migrationBuilder.RenameColumn(
                name: "AttributeDefinitionId",
                table: "FloatMetadata",
                newName: "AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_FloatMetadata_AttributeDefinitionId",
                table: "FloatMetadata",
                newName: "IX_FloatMetadata_AttributeId");

            migrationBuilder.RenameColumn(
                name: "AttributeDefinitionId",
                table: "DateMetadata",
                newName: "AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_DateMetadata_AttributeDefinitionId",
                table: "DateMetadata",
                newName: "IX_DateMetadata_AttributeId");

            migrationBuilder.RenameColumn(
                name: "AttributeDefinitionId",
                table: "BlobMetadata",
                newName: "AttributeId");

            migrationBuilder.RenameIndex(
                name: "IX_BlobMetadata_AttributeDefinitionId",
                table: "BlobMetadata",
                newName: "IX_BlobMetadata_AttributeId");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "TextMetadata",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "IntegerMetadata",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "FloatMetadata",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "DateMetadata",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "BlobMetadata",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "FK_BlobMetadata_MetadataAttributes_AttributeId",
                table: "BlobMetadata",
                column: "AttributeId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DateMetadata_MetadataAttributes_AttributeId",
                table: "DateMetadata",
                column: "AttributeId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloatMetadata_MetadataAttributes_AttributeId",
                table: "FloatMetadata",
                column: "AttributeId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegerMetadata_MetadataAttributes_AttributeId",
                table: "IntegerMetadata",
                column: "AttributeId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMetadata_MetadataAttributes_AttributeId",
                table: "TextMetadata",
                column: "AttributeId",
                principalTable: "MetadataAttributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
