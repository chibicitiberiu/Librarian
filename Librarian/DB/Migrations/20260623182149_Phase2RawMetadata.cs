using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class Phase2RawMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawMetadataAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    SubResourceId = table.Column<int>(type: "integer", nullable: true),
                    Namespace = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    DateUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMetadataAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMetadataAttributes_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RawMetadataAttributes_SubResources_SubResourceId",
                        column: x => x.SubResourceId,
                        principalTable: "SubResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawMetadataAttributes_FileId",
                table: "RawMetadataAttributes",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMetadataAttributes_Namespace_Key",
                table: "RawMetadataAttributes",
                columns: new[] { "Namespace", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_RawMetadataAttributes_SubResourceId",
                table: "RawMetadataAttributes",
                column: "SubResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMetadataAttributes");
        }
    }
}
