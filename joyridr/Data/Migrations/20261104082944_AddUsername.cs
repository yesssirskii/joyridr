using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joyridr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the column, empty for now.
            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // 2. Give every existing account one, from its name: "Ivan Kovač" -> "ivan.kovac".
            //    Accented letters become plain ones, anything else that isn't allowed is dropped,
            //    a name with nothing usable left becomes "user", and a second "ivan.kovac"
            //    (by sign-up date) becomes "ivan.kovac2".
            migrationBuilder.Sql("""
                WITH cleaned AS (
                    SELECT id, created_at,
                           trim(both '.' from left(regexp_replace(
                               translate(lower(first_name || '.' || last_name),
                                         'čćžšđáàâäãåéèêëíìîïóòôöõúùûüýÿñç',
                                         'cczsdaaaaaaeeeeiiiiooooouuuuyync'),
                               '[^a-z0-9._]', '', 'g'), 16)) AS name
                    FROM users
                ),
                base AS (
                    SELECT id, created_at, CASE WHEN length(name) >= 3 THEN name ELSE 'user' END AS name
                    FROM cleaned
                ),
                numbered AS (
                    SELECT id, name, row_number() OVER (PARTITION BY name ORDER BY created_at, id) AS n
                    FROM base
                )
                UPDATE users
                SET username = CASE WHEN numbered.n = 1 THEN numbered.name ELSE numbered.name || numbered.n END
                FROM numbered
                WHERE numbered.id = users.id;
                """);

            // 3. Now that everyone has one, make it required and unique.
            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");
        }
    }
}
