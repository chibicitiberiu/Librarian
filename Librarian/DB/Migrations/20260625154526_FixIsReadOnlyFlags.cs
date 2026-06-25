using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class FixIsReadOnlyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 26,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 29,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 30,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 31,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 32,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 52,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 53,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 79,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 80,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 81,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 82,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 84,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 85,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 88,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 89,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 105,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 106,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 119,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 120,
                column: "IsReadOnly",
                value: true);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 121,
                column: "IsReadOnly",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 18,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 24,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 25,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 26,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 29,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 30,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 31,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 32,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 52,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 53,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 79,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 80,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 81,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 82,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 84,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 85,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 88,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 89,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 105,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 106,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 119,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 120,
                column: "IsReadOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 121,
                column: "IsReadOnly",
                value: false);
        }
    }
}
