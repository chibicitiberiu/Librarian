using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class ChecksumSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PrefixHash",
                table: "IndexedFiles",
                type: "bytea",
                nullable: true);

            migrationBuilder.InsertData(
                table: "AttributeDefinitions",
                columns: new[] { "Id", "Description", "Group", "IsReadOnly", "Name", "Type", "Unit" },
                values: new object[] { 121, "SHA-256 content hash (hex).", "File attributes", false, "Checksum", 0, "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 121);

            migrationBuilder.DropColumn(
                name: "PrefixHash",
                table: "IndexedFiles");
        }
    }
}
