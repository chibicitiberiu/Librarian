using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class Addeditablefieldtometadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Editable",
                table: "TextMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "TextMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Grouping",
                table: "MetadataAttributes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Editable",
                table: "IntegerMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "IntegerMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Editable",
                table: "FloatMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "FloatMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Editable",
                table: "DateMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "DateMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Editable",
                table: "BlobMetadata",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "BlobMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Editable",
                table: "TextMetadata");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "TextMetadata");

            migrationBuilder.DropColumn(
                name: "Grouping",
                table: "MetadataAttributes");

            migrationBuilder.DropColumn(
                name: "Editable",
                table: "IntegerMetadata");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "IntegerMetadata");

            migrationBuilder.DropColumn(
                name: "Editable",
                table: "FloatMetadata");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "FloatMetadata");

            migrationBuilder.DropColumn(
                name: "Editable",
                table: "DateMetadata");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "DateMetadata");

            migrationBuilder.DropColumn(
                name: "Editable",
                table: "BlobMetadata");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "BlobMetadata");
        }
    }
}
