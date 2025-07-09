using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DayvpnBotWebApi.Migrations
{
    /// <inheritdoc />
    public partial class update_service_subscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubscriptionVolumeMb",
                table: "Subscriptions",
                newName: "SubscriptionVolumeGb");

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ServiceId",
                table: "Subscriptions",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Services_ServiceId",
                table: "Subscriptions",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Services_ServiceId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_ServiceId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "Subscriptions");

            migrationBuilder.RenameColumn(
                name: "SubscriptionVolumeGb",
                table: "Subscriptions",
                newName: "SubscriptionVolumeMb");
        }
    }
}
