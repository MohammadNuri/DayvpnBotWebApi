using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DayvpnBotWebApi.Migrations
{
    /// <inheritdoc />
    public partial class updateuser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppLog_Subscription_SubscriptionId",
                table: "AppLog");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLog_User_UserId",
                table: "AppLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscription_User_UserId",
                table: "Subscription");

            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionLinks_Subscription_SubscriptionId",
                table: "SubscriptionLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Subscription",
                table: "Subscription");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppLog",
                table: "AppLog");

            migrationBuilder.RenameTable(
                name: "User",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "Subscription",
                newName: "Subscriptions");

            migrationBuilder.RenameTable(
                name: "AppLog",
                newName: "AppLogs");

            migrationBuilder.RenameIndex(
                name: "IX_Subscription_UserId",
                table: "Subscriptions",
                newName: "IX_Subscriptions_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppLog_UserId",
                table: "AppLogs",
                newName: "IX_AppLogs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppLog_SubscriptionId",
                table: "AppLogs",
                newName: "IX_AppLogs_SubscriptionId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Users",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Subscriptions",
                table: "Subscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppLogs",
                table: "AppLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppLogs_Subscriptions_SubscriptionId",
                table: "AppLogs",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLogs_Users_UserId",
                table: "AppLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionLinks_Subscriptions_SubscriptionId",
                table: "SubscriptionLinks",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                table: "Subscriptions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppLogs_Subscriptions_SubscriptionId",
                table: "AppLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AppLogs_Users_UserId",
                table: "AppLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionLinks_Subscriptions_SubscriptionId",
                table: "SubscriptionLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                table: "Subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Subscriptions",
                table: "Subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppLogs",
                table: "AppLogs");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "User");

            migrationBuilder.RenameTable(
                name: "Subscriptions",
                newName: "Subscription");

            migrationBuilder.RenameTable(
                name: "AppLogs",
                newName: "AppLog");

            migrationBuilder.RenameIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscription",
                newName: "IX_Subscription_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppLogs_UserId",
                table: "AppLog",
                newName: "IX_AppLog_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppLogs_SubscriptionId",
                table: "AppLog",
                newName: "IX_AppLog_SubscriptionId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "User",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Subscription",
                table: "Subscription",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppLog",
                table: "AppLog",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppLog_Subscription_SubscriptionId",
                table: "AppLog",
                column: "SubscriptionId",
                principalTable: "Subscription",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLog_User_UserId",
                table: "AppLog",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscription_User_UserId",
                table: "Subscription",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionLinks_Subscription_SubscriptionId",
                table: "SubscriptionLinks",
                column: "SubscriptionId",
                principalTable: "Subscription",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
