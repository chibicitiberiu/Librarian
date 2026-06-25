using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddContainmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "TextAttributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "TextAttributes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentCollectionId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "IntegerAttributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "IntegerAttributes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalPath",
                table: "IndexedFiles",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentFileId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "IndexedFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "FloatAttributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "FloatAttributes",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "DateAttributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "DateAttributes",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "BlobAttributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "BlobAttributes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentCollectionId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RoleSource = table.Column<int>(type: "integer", nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Collections_ParentCollectionId",
                        column: x => x.ParentCollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TextAttributes_CollectionId",
                table: "TextAttributes",
                column: "CollectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TextAttributes_Owner",
                table: "TextAttributes",
                sql: "num_nonnulls(\"FileId\", \"CollectionId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ParentCollectionId",
                table: "Items",
                column: "ParentCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegerAttributes_CollectionId",
                table: "IntegerAttributes",
                column: "CollectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IntegerAttributes_Owner",
                table: "IntegerAttributes",
                sql: "num_nonnulls(\"FileId\", \"CollectionId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_CollectionId",
                table: "IndexedFiles",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_ParentFileId_InternalPath",
                table: "IndexedFiles",
                columns: new[] { "ParentFileId", "InternalPath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FloatAttributes_CollectionId",
                table: "FloatAttributes",
                column: "CollectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FloatAttributes_Owner",
                table: "FloatAttributes",
                sql: "num_nonnulls(\"FileId\", \"CollectionId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DateAttributes_CollectionId",
                table: "DateAttributes",
                column: "CollectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DateAttributes_Owner",
                table: "DateAttributes",
                sql: "num_nonnulls(\"FileId\", \"CollectionId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BlobAttributes_CollectionId",
                table: "BlobAttributes",
                column: "CollectionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BlobAttributes_Owner",
                table: "BlobAttributes",
                sql: "num_nonnulls(\"FileId\", \"CollectionId\") = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ParentCollectionId",
                table: "Collections",
                column: "ParentCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_SourcePath",
                table: "Collections",
                column: "SourcePath");

            migrationBuilder.AddForeignKey(
                name: "FK_BlobAttributes_Collections_CollectionId",
                table: "BlobAttributes",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DateAttributes_Collections_CollectionId",
                table: "DateAttributes",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloatAttributes_Collections_CollectionId",
                table: "FloatAttributes",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_Collections_CollectionId",
                table: "IndexedFiles",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_IndexedFiles_ParentFileId",
                table: "IndexedFiles",
                column: "ParentFileId",
                principalTable: "IndexedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegerAttributes_Collections_CollectionId",
                table: "IntegerAttributes",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Collections_ParentCollectionId",
                table: "Items",
                column: "ParentCollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TextAttributes_Collections_CollectionId",
                table: "TextAttributes",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlobAttributes_Collections_CollectionId",
                table: "BlobAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_DateAttributes_Collections_CollectionId",
                table: "DateAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_FloatAttributes_Collections_CollectionId",
                table: "FloatAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_Collections_CollectionId",
                table: "IndexedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_IndexedFiles_ParentFileId",
                table: "IndexedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegerAttributes_Collections_CollectionId",
                table: "IntegerAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Collections_ParentCollectionId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_TextAttributes_Collections_CollectionId",
                table: "TextAttributes");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_TextAttributes_CollectionId",
                table: "TextAttributes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TextAttributes_Owner",
                table: "TextAttributes");

            migrationBuilder.DropIndex(
                name: "IX_Items_ParentCollectionId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_IntegerAttributes_CollectionId",
                table: "IntegerAttributes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IntegerAttributes_Owner",
                table: "IntegerAttributes");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_CollectionId",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_ParentFileId_InternalPath",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_FloatAttributes_CollectionId",
                table: "FloatAttributes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FloatAttributes_Owner",
                table: "FloatAttributes");

            migrationBuilder.DropIndex(
                name: "IX_DateAttributes_CollectionId",
                table: "DateAttributes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DateAttributes_Owner",
                table: "DateAttributes");

            migrationBuilder.DropIndex(
                name: "IX_BlobAttributes_CollectionId",
                table: "BlobAttributes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BlobAttributes_Owner",
                table: "BlobAttributes");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "TextAttributes");

            migrationBuilder.DropColumn(
                name: "ParentCollectionId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "IntegerAttributes");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "InternalPath",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "ParentFileId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "FloatAttributes");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "DateAttributes");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "BlobAttributes");

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "TextAttributes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "IntegerAttributes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "FloatAttributes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "DateAttributes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FileId",
                table: "BlobAttributes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
