using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarBookingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurePaymentDateTimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_TableId",
                schema: "public",
                table: "Bookings");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_UserId",
                schema: "public",
                table: "Bookings",
                newName: "IX_Booking_UserId");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumSpend",
                schema: "public",
                table: "PromoCodes",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                schema: "public",
                table: "PromoCodes",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                schema: "public",
                table: "PromoCodes",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "RefundAmount",
                schema: "public",
                table: "Payments",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "StartTime",
                schema: "public",
                table: "Bookings",
                type: "time",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "EndTime",
                schema: "public",
                table: "Bookings",
                type: "time",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingDate",
                schema: "public",
                table: "Bookings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCode_Validity",
                schema: "public",
                table: "PromoCodes",
                columns: new[] { "IsActive", "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_TableId_Date",
                schema: "public",
                table: "Bookings",
                columns: new[] { "TableId", "BookingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromoCode_Validity",
                schema: "public",
                table: "PromoCodes");

            migrationBuilder.DropIndex(
                name: "IX_Booking_TableId_Date",
                schema: "public",
                table: "Bookings");

            migrationBuilder.RenameIndex(
                name: "IX_Booking_UserId",
                schema: "public",
                table: "Bookings",
                newName: "IX_Bookings_UserId");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinimumSpend",
                schema: "public",
                table: "PromoCodes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                schema: "public",
                table: "PromoCodes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                schema: "public",
                table: "PromoCodes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "RefundAmount",
                schema: "public",
                table: "Payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "StartTime",
                schema: "public",
                table: "Bookings",
                type: "interval",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "EndTime",
                schema: "public",
                table: "Bookings",
                type: "interval",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time");

            migrationBuilder.AlterColumn<DateTime>(
                name: "BookingDate",
                schema: "public",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TableId",
                schema: "public",
                table: "Bookings",
                column: "TableId");
        }
    }
}
