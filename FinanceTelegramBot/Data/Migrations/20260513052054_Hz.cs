using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTelegramBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Hz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_money_transactions_user_id",
                table: "money_transactions",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_money_transactions_users_user_id",
                table: "money_transactions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_money_transactions_users_user_id",
                table: "money_transactions");

            migrationBuilder.DropIndex(
                name: "ix_money_transactions_user_id",
                table: "money_transactions");
        }
    }
}
