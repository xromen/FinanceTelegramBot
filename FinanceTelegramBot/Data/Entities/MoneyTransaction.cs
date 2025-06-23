using System.ComponentModel.DataAnnotations;
using Telegram.Bot.Types;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Data.Entities;

public class MoneyTransaction : BaseEntity
{
    [Required]
    public decimal Amount { get; set; }

    [Required]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Required]
    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required]
    public long UserId { get; set; }

    public string? CheckQrCodeRaw { get; set; }

    public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
