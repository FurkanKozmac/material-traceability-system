using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddLowStockNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "alert_email_recipient",
                table: "system_settings",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "alerts@mts.local");

            migrationBuilder.AddColumn<bool>(
                name: "enable_low_stock_alerts",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "min_stock_threshold",
                table: "chemicals",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "alert_email_recipient", "enable_low_stock_alerts" },
                values: new object[] { "alerts@mts.local", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alert_email_recipient",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "enable_low_stock_alerts",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "min_stock_threshold",
                table: "chemicals");
        }
    }
}
