using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Librarian.DB.Migrations
{
    /// <summary>
    /// Splits the <c>AttributeDefinitions</c> identity space so seed-defined ("known") attributes and
    /// runtime-curated ("Other") attributes can never collide on the primary key.
    ///
    /// The seed (HasData from MetadataAttributes.csv) uses explicit ids 1..N, and Npgsql leaves the
    /// identity sequence pointing just past them — so the first runtime-curated attribute squats on the
    /// very next id a future CSV append would claim (this bit us once, worked around by reusing
    /// General.Collection for TV "Series"). After this migration, ids below <c>CurationIdBase</c>
    /// (= 1,000,000) are reserved for the seed, and curation always lands at 1,000,000+.
    ///
    /// Curated rows are part of the rebuildable cache (they're recreated from the raw layer by
    /// `renormalize`), so any currently squatting in the reserved range are cleared here and re-minted
    /// in the curation range on the next normalization pass. See plan.md Phase 4.
    /// </summary>
    public partial class ReserveCurationIdSpace : Migration
    {
        private const long CurationIdBase = 1_000_000;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Vacate the reserved seed range. The value tables FK to AttributeDefinitions with
            // ON DELETE CASCADE, so their dependent rows go too; no alias points at an "Other" def.
            migrationBuilder.Sql(
                $"DELETE FROM \"AttributeDefinitions\" WHERE \"Group\" = 'Other' AND \"Id\" < {CurationIdBase};");

            // All future runtime-curated definitions get ids at or above the curation base.
            migrationBuilder.Sql(
                $"ALTER TABLE \"AttributeDefinitions\" ALTER COLUMN \"Id\" RESTART WITH {CurationIdBase};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hand the sequence back to just past the existing rows. Deleted curated rows are not
            // restored (they were cache); the next normalization recreates them.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('\"AttributeDefinitions\"', 'Id'), " +
                "GREATEST((SELECT MAX(\"Id\") FROM \"AttributeDefinitions\"), 1));");
        }
    }
}
