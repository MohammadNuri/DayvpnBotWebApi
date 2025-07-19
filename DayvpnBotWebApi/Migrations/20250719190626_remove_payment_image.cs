using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DayvpnBotWebApi.Migrations
{
    /// <inheritdoc />
    public partial class remove_payment_image : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentImage",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PaymentImage",
                table: "TransactionRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PaymentImage",
                table: "Transactions",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PaymentImage",
                table: "TransactionRequests",
                type: "varbinary(max)",
                nullable: true);
        }
    }
}
