using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RequireRackCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE addresses SET max_capacity = 1 WHERE max_capacity IS NULL OR max_capacity <= 0;");

            migrationBuilder.AlterColumn<int>(
                name: "max_capacity",
                table: "addresses",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "max_capacity",
                table: "addresses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
