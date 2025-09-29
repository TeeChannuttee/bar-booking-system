using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarBookingSystem.Migrations
{
    /// <inheritdoc />
    public partial class SetPaymentTransactionDetailsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TransactionDetails",
                schema: "public",
                table: "Payments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TransactionDetails",
                schema: "public",
                table: "Payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'{}'::jsonb");
        }
    }
}
