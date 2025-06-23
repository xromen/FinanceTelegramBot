using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace FinanceTelegramBot.Base.Models;
public class UserState
{
    public long UserId { get; set; }
    public Func<Update, UserState, IServiceScope, Task> Action { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
