using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class M3cItemEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: hand-corrected from EF's auto-generated RenameColumn. PrimaryFileId pointed at
            // IndexedFiles; ItemId points at Items. Renaming would keep stale file-ids as item-ids and
            // violate the new FK — so drop the old column and add a fresh (null) ItemId instead.
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "PrimaryFileId",
                table: "IndexedFiles");

            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_ItemId",
                table: "IndexedFiles",
                column: "ItemId");

            migrationBuilder.AddColumn<int>(
                name: "RoleSource",
                table: "IndexedFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleSource = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_Items_ItemId",
                table: "IndexedFiles",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_Items_ItemId",
                table: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropColumn(
                name: "RoleSource",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_ItemId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "IndexedFiles");

            migrationBuilder.AddColumn<int>(
                name: "PrimaryFileId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

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
    }
}
