using Telegram.Bot.Types;

namespace FinanceTelegramBot.Models;

public class RouteEnvironment
{
    public long UserId { get; set; }
    public Update Update { get; set; }
}
