using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class Phase3SearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TextAttributes_ValueSearch",
                table: "TextAttributes",
                column: "ValueSearch")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFileContents_ContentSearch",
                table: "IndexedFileContents",
                column: "ContentSearch")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextAttributes_ValueSearch",
                table: "TextAttributes");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFileContents_ContentSearch",
                table: "IndexedFileContents");
        }
    }
}
