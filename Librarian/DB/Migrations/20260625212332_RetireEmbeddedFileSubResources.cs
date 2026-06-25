using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <inheritdoc />
    public partial class RetireEmbeddedFileSubResources : Migration
    {
        // Archive entries used to be stored as EmbeddedFile sub-resources glued onto the archive file
        // (SubResourceKind.EmbeddedFile = 3), capped at 100/archive. They are now materialized as their
        // own virtual files (collection_plan.md §7), so the old rows and their attributes are stale and
        // rebuilt on the next reindex. Delete the EmbeddedFile sub-resources and everything keyed to them.
        // The enum value is kept for back-compat reads, but no live path produces it any more.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string subResourceIds = "SELECT \"Id\" FROM \"SubResources\" WHERE \"Kind\" = 3";

            foreach (var table in new[]
                { "RawMetadataAttributes", "TextAttributes", "IntegerAttributes",
                  "FloatAttributes", "DateAttributes", "BlobAttributes" })
            {
                migrationBuilder.Sql(
                    $"DELETE FROM \"{table}\" WHERE \"SubResourceId\" IN ({subResourceIds});");
            }

            migrationBuilder.Sql("DELETE FROM \"SubResources\" WHERE \"Kind\" = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data cleanup: the deleted rows are reconstructed by re-indexing, not by a down-migration.
        }
    }
}
