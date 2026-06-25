using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class IgnoreTlenDurationAlias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeAliases",
                keyColumn: "Id",
                keyValue: 85,
                columns: new[] { "AttributeDefinitionId", "Role" },
                values: new object[] { null, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeAliases",
                keyColumn: "Id",
                keyValue: 85,
                columns: new[] { "AttributeDefinitionId", "Role" },
                values: new object[] { 88, 0 });
        }
    }
}
