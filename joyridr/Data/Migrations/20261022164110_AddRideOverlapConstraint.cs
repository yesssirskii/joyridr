using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joyridr.Data.Migrations
{
    /// <summary>
    /// A driver can't be in two places at once: the database rejects any two rides for the same
    /// driver whose times overlap, as long as both are still active. This is the safety net behind
    /// the availability check in the app (which can race when two riders book at the same moment).
    /// EF can't model exclusion constraints, so this is hand-written. Keep the status list in sync
    /// with Ride.BlockingStatuses.
    /// </summary>
    public partial class AddRideOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE rides
                ADD CONSTRAINT ex_rides_driver_no_overlap
                EXCLUDE USING gist (
                    driver_id WITH =,
                    tstzrange(starts_at, ends_at, '[)') WITH &&
                )
                WHERE (status IN ('AwaitingPayment', 'Requested', 'Confirmed', 'InProgress'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE rides DROP CONSTRAINT IF EXISTS ex_rides_driver_no_overlap;");
        }
    }
}
