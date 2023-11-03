using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class Initialmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    NeedsUpdating = table.Column<bool>(type: "boolean", nullable: false),
                    IndexLastUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataAttributes", x => x.Id);
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
                name: "BlobMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobMetadata_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlobMetadata_MetadataAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DateMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DateMetadata_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DateMetadata_MetadataAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FloatMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloatMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloatMetadata_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FloatMetadata_MetadataAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegerMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegerMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegerMetadata_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegerMetadata_MetadataAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TextMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueSearch = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextMetadata_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TextMetadata_MetadataAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "MetadataAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlobMetadata_AttributeId",
                table: "BlobMetadata",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobMetadata_FileId",
                table: "BlobMetadata",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_DateMetadata_AttributeId",
                table: "DateMetadata",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_DateMetadata_FileId",
                table: "DateMetadata",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatMetadata_AttributeId",
                table: "FloatMetadata",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_FloatMetadata_FileId",
                table: "FloatMetadata",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Path",
                table: "IndexedFiles",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegerMetadata_AttributeId",
                table: "IntegerMetadata",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegerMetadata_FileId",
                table: "IntegerMetadata",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMetadata_AttributeId",
                table: "TextMetadata",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMetadata_FileId",
                table: "TextMetadata",
                column: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlobMetadata");

            migrationBuilder.DropTable(
                name: "DateMetadata");

            migrationBuilder.DropTable(
                name: "FloatMetadata");

            migrationBuilder.DropTable(
                name: "IndexedFileContents");

            migrationBuilder.DropTable(
                name: "IntegerMetadata");

            migrationBuilder.DropTable(
                name: "TextMetadata");

            migrationBuilder.DropTable(
                name: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "MetadataAttributes");
        }
    }
}
