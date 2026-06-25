using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeNumericUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 18,
                column: "Unit",
                value: "Hz");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 32,
                column: "Unit",
                value: "bytes");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 84,
                column: "Unit",
                value: "bps");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 119,
                column: "Unit",
                value: "fps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 18,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 32,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 84,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 119,
                column: "Unit",
                value: "");
        }
    }
}
