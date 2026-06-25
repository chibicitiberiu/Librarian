using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class M3aSidecarAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrimaryFileId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "IndexedFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles",
                column: "PrimaryFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles",
                column: "PrimaryFileId",
                principalTable: "IndexedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "IndexedFiles");
        }
    }
}
