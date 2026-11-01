using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joyridr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "remind_driver_at",
                table: "rides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_rides_status_remind_driver_at",
                table: "rides",
                columns: new[] { "status", "remind_driver_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rides_status_remind_driver_at",
                table: "rides");

            migrationBuilder.DropColumn(
                name: "remind_driver_at",
                table: "rides");
        }
    }
}
