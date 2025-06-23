using System.ComponentModel.DataAnnotations;

namespace FinanceTelegramBot.Data.Entities;
public class CategoryKeyword : BaseEntity
{
    [Required]
    public string Keyword { get; set; } = null!;
    [Required]
    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
