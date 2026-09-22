using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokensAndQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_units_address_id",
                table: "units");

            migrationBuilder.DropIndex(
                name: "IX_batches_chemical_id",
                table: "batches");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_units_address_id_status",
                table: "units",
                columns: new[] { "address_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_units_status_batch_id",
                table: "units",
                columns: new[] { "status", "batch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_status_created_at",
                table: "relocation_tasks",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_batches_chemical_id_expiration_date",
                table: "batches",
                columns: new[] { "chemical_id", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId_ExpiresAt",
                table: "refresh_tokens",
                columns: new[] { "UserId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_units_address_id_status",
                table: "units");

            migrationBuilder.DropIndex(
                name: "IX_units_status_batch_id",
                table: "units");

            migrationBuilder.DropIndex(
                name: "IX_relocation_tasks_status_created_at",
                table: "relocation_tasks");

            migrationBuilder.DropIndex(
                name: "IX_batches_chemical_id_expiration_date",
                table: "batches");

            migrationBuilder.CreateIndex(
                name: "IX_units_address_id",
                table: "units",
                column: "address_id");

            migrationBuilder.CreateIndex(
                name: "IX_batches_chemical_id",
                table: "batches",
                column: "chemical_id");
        }
    }
}
