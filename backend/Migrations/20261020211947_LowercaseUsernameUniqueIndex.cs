using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseUsernameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                return;

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    conflicts text;
                BEGIN
                    SELECT string_agg(grouped."Variants", ', ')
                    INTO conflicts
                    FROM (
                        SELECT string_agg("Username", ' / ' ORDER BY "Username") AS "Variants"
                        FROM users
                        GROUP BY lower("Username")
                        HAVING count(*) > 1
                    ) AS grouped;

                    IF conflicts IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Cannot create ux_users_username_lower: case-insensitive username conflicts exist: %',
                            conflicts;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_users_username_lower
                ON users (lower("Username"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                return;

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_users_username_lower;");
        }
    }
}
