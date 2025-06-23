using System.ComponentModel.DataAnnotations;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Data.Entities;
public class Category : BaseEntity
{
    [Required]
    public long UserId { get; set; }
    [Required]
    public string Name { get; set; } = null!;
    [Required]
    public TransactionType Type { get; set; }
    public List<CategoryKeyword> Keywords { get; set; } = new List<CategoryKeyword>();
}

