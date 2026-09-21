using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class CoreSchemaTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy tables if they exist. Renaming first lets EF create the target schema normally.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'addresses') THEN
                        ALTER TABLE addresses RENAME TO legacy_addresses;
                        ALTER TABLE batches RENAME TO legacy_batches;
                        ALTER TABLE chemicals RENAME TO legacy_chemicals;
                        ALTER TABLE relocation_tasks RENAME TO legacy_relocation_tasks;
                        ALTER TABLE roles RENAME TO legacy_roles;
                        ALTER TABLE units RENAME TO legacy_units;
                        ALTER TABLE users RENAME TO legacy_users;
                        ALTER TABLE user_roles RENAME TO legacy_user_roles;

                        DROP TABLE IF EXISTS
                            address_sectors,
                            address_types,
                            barrel_picking_session_units,
                            barrel_picking_sessions,
                            hms_picking_session_units,
                            hms_picking_sessions,
                            hms_receive_records,
                            ms_picking_session_units,
                            ms_picking_sessions,
                            ms_receive_records,
                            permissions,
                            refresh_tokens,
                            relife_requests,
                            reuse_requests,
                            role_permissions,
                            shops,
                            stock_limits,
                            tanker_load_records,
                            tracking_points,
                            user_shops,
                            vehicle_flows,
                            vehicles
                        CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "addresses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chemicals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    chemical_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    msds_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    stopped = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chemicals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    auth_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batch_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    initial_quantity = table.Column<int>(type: "integer", nullable: false),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    chemical_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batches_chemicals_chemical_id",
                        column: x => x.chemical_id,
                        principalTable: "chemicals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    RolesId = table.Column<long>(type: "bigint", nullable: false),
                    UsersId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.RolesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    address_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_units_addresses_address_id",
                        column: x => x.address_id,
                        principalTable: "addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_units_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "relocation_tasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unit_id = table.Column<long>(type: "bigint", nullable: false),
                    from_address_id = table.Column<long>(type: "bigint", nullable: false),
                    to_address_id = table.Column<long>(type: "bigint", nullable: false),
                    completed_by_user_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relocation_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_relocation_tasks_addresses_from_address_id",
                        column: x => x.from_address_id,
                        principalTable: "addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relocation_tasks_addresses_to_address_id",
                        column: x => x.to_address_id,
                        principalTable: "addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relocation_tasks_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_relocation_tasks_users_completed_by_user_id",
                        column: x => x.completed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_addresses_code",
                table: "addresses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batches_batch_no",
                table: "batches",
                column: "batch_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batches_chemical_id",
                table: "batches",
                column: "chemical_id");

            migrationBuilder.CreateIndex(
                name: "IX_chemicals_chemical_code",
                table: "chemicals",
                column: "chemical_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_completed_by_user_id",
                table: "relocation_tasks",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_from_address_id",
                table: "relocation_tasks",
                column: "from_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_to_address_id",
                table: "relocation_tasks",
                column: "to_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_relocation_tasks_unit_id",
                table: "relocation_tasks",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_units_address_id",
                table: "units",
                column: "address_id");

            migrationBuilder.CreateIndex(
                name: "IX_units_barcode",
                table: "units",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_batch_id",
                table: "units",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UsersId",
                table: "user_roles",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'legacy_addresses') THEN
                        INSERT INTO addresses ("Id", code, max_capacity)
                        SELECT id, code, max_capacity
                        FROM legacy_addresses;

                        INSERT INTO chemicals ("Id", "Name", chemical_code, description, msds_url, stopped)
                        SELECT id, name, chemical_code, description, msds_url, COALESCE(stopped, false)
                        FROM legacy_chemicals;

                        INSERT INTO roles ("Id", "Name")
                        SELECT id, name
                        FROM legacy_roles;

                        INSERT INTO users ("Id", "Username", "Password", auth_source, active)
                        SELECT id, username, COALESCE(password, ''), auth_source, active
                        FROM legacy_users;

                        INSERT INTO batches ("Id", batch_no, supplier, initial_quantity, expiration_date, chemical_id)
                        SELECT
                            id,
                            batch_no,
                            supplier,
                            initial_quantity,
                            expiration_date::timestamp AT TIME ZONE 'UTC',
                            chemical_id
                        FROM legacy_batches;

                        INSERT INTO user_roles ("RolesId", "UsersId")
                        SELECT role_id, user_id
                        FROM legacy_user_roles;

                        INSERT INTO units ("Id", barcode, status, version, consumed_at, batch_id, address_id)
                        SELECT
                            id,
                            barcode,
                            status,
                            version,
                            consumed_at AT TIME ZONE 'UTC',
                            batch_id,
                            address_id
                        FROM legacy_units;

                        INSERT INTO relocation_tasks (
                            "Id",
                            status,
                            created_at,
                            completed_at,
                            unit_id,
                            from_address_id,
                            to_address_id,
                            completed_by_user_id)
                        SELECT
                            id,
                            status,
                            created_at AT TIME ZONE 'UTC',
                            completed_at AT TIME ZONE 'UTC',
                            unit_id,
                            from_address_id,
                            to_address_id,
                            completed_by_user_id
                        FROM legacy_relocation_tasks;

                        PERFORM setval(pg_get_serial_sequence('addresses', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM addresses;
                        PERFORM setval(pg_get_serial_sequence('batches', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM batches;
                        PERFORM setval(pg_get_serial_sequence('chemicals', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM chemicals;
                        PERFORM setval(pg_get_serial_sequence('relocation_tasks', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM relocation_tasks;
                        PERFORM setval(pg_get_serial_sequence('roles', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM roles;
                        PERFORM setval(pg_get_serial_sequence('units', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM units;
                        PERFORM setval(pg_get_serial_sequence('users', 'Id'), COALESCE(MAX("Id"), 1), MAX("Id") IS NOT NULL) FROM users;

                        DROP TABLE
                            legacy_addresses,
                            legacy_batches,
                            legacy_chemicals,
                            legacy_relocation_tasks,
                            legacy_roles,
                            legacy_units,
                            legacy_users,
                            legacy_user_roles
                        CASCADE;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relocation_tasks");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "addresses");

            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "chemicals");
        }
    }
}
