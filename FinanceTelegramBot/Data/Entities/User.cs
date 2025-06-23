using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FinanceTelegramBot.Data.Entities;

public class User : BaseEntity
{
    public string? Username { get; set; }

    public string FirstName { get; set; }

    public string? LastName { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    public long? FamilyId { get; set; }

    [ForeignKey(nameof(FamilyId))]
    public Family? Family { get; set; }
}
