using FinanceTelegramBot.Data.Entities;

namespace FinanceTelegramBot.Models;

public class TransactionDto
{
    public decimal Amount { get; set; }
    public string? Keyword { get; set; }
    public DateOnly Date { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public string? CheckQrCodeRaw { get; set; }
    public List<PurchaseItem>? Items { get; set; }
}
