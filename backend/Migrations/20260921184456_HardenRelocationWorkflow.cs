using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class HardenRelocationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH duplicate_tasks AS (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY unit_id ORDER BY "Id" DESC) AS row_number
                    FROM relocation_tasks
                    WHERE status = 'Pending'
                )
                UPDATE relocation_tasks
                SET status = 'Cancelled', completed_at = NOW()
                WHERE "Id" IN (SELECT "Id" FROM duplicate_tasks WHERE row_number > 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks");

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
            migrationBuilder.DropIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks");

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks",
                column: "unit_id");
        }
    }
}
