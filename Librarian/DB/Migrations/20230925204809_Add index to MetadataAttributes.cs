using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddindextoMetadataAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MetadataAttributes_Grouping_Name_Type",
                table: "MetadataAttributes",
                columns: new[] { "Grouping", "Name", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataAttributes_Grouping_Name_Type",
                table: "MetadataAttributes");
        }
    }
}
