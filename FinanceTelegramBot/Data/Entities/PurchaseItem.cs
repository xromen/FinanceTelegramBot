using System.ComponentModel.DataAnnotations;

namespace FinanceTelegramBot.Data.Entities;

public class PurchaseItem : BaseEntity
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public decimal Price { get; set; }

    [Required]
    public decimal Quantity { get; set; }

    public long MoneyTransactionId { get; set; }
}
