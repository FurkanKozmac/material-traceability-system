using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeRelocationStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks");

            migrationBuilder.Sql("""
                WITH duplicate_tasks AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY unit_id ORDER BY "Id" DESC) AS row_number
                    FROM relocation_tasks
                    WHERE UPPER(status) = 'PENDING'
                )
                UPDATE relocation_tasks
                SET status = 'Cancelled', completed_at = NOW()
                WHERE "Id" IN (SELECT "Id" FROM duplicate_tasks WHERE row_number > 1);

                UPDATE relocation_tasks SET status = 'Pending' WHERE UPPER(status) = 'PENDING';
                UPDATE relocation_tasks SET status = 'Completed' WHERE UPPER(status) = 'COMPLETED';
                UPDATE relocation_tasks SET status = 'Cancelled' WHERE UPPER(status) = 'CANCELLED';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks",
                column: "unit_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Status casing is intentionally not reverted.
        }
    }
}
