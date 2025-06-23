using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTelegramBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMoneyTransactionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "transaction_type",
                table: "money_transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "transaction_type",
                table: "money_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
