using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageTypeRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "storage_type",
                table: "chemicals",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GENERAL");

            migrationBuilder.AddColumn<string>(
                name: "storage_type",
                table: "addresses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GENERAL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "storage_type",
                table: "chemicals");

            migrationBuilder.DropColumn(
                name: "storage_type",
                table: "addresses");
        }
    }
}
