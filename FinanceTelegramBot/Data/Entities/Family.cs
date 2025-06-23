using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telegram.Bot.Types;

namespace FinanceTelegramBot.Data.Entities;

public class Family : BaseEntity
{
    [Required]
    [ForeignKey(nameof(Owner))]
    public long OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    [InverseProperty(nameof(User.Family))]
    public virtual ICollection<User> Members { get; set; } = new List<User>();
}
