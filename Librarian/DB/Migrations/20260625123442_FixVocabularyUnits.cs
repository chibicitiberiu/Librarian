using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class FixVocabularyUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 7,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 9,
                column: "Unit",
                value: "bpm");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 15,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 83,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 110,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 111,
                column: "Unit",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 7,
                column: "Unit",
                value: "dB");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 9,
                column: "Unit",
                value: "");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Unit",
                value: "bpm");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 15,
                column: "Unit",
                value: "bps");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 83,
                column: "Unit",
                value: "dB");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 110,
                column: "Unit",
                value: "dB");

            migrationBuilder.UpdateData(
                table: "AttributeDefinitions",
                keyColumn: "Id",
                keyValue: 111,
                column: "Unit",
                value: "dB");
        }
    }
}
