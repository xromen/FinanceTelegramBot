using Telegram.Bot.Types;

namespace FinanceTelegramBot.Base.Services;
public class NavigationService
{
    private static readonly Dictionary<long, Stack<Update>> _userStacks = new();

    public void Push(Update update, long userId)
    {
        if (!_userStacks.ContainsKey(userId))
            _userStacks[userId] = new Stack<Update>();

        _userStacks[userId].Push(update);
    }

    public Update? Pop(long userId)
    {
        if (_userStacks.TryGetValue(userId, out var stack) && stack.Count > 1)
        {
            stack.Pop();
            return stack.Pop();
        }
        return null;
    }

    public Update? GetCurrent(long userId)
    {
        if (_userStacks.TryGetValue(userId, out var stack) && stack.Count > 0)
            return stack.Peek();
        return null;
    }
}
