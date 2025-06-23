using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Services;

namespace FinanceTelegramBot.Controllers;

[TelegramRoute("/[controller]/[action]")]
public class NavigationController(
    NavigationService navigationService, 
    RouteEnvironment env, 
    StateService stateService, 
    TelegramRouter router, 
    DefaultCommandService defaultCommandService
    ) : ITelegramController
{
    public async Task Back()
    {
        var prevUpdate = navigationService.Pop(env.UserId);

        var state = stateService.GetCurrentState(env.UserId);
        if (state != null)
        {
            stateService.RemoveState(state);
        }

        string data = string.Empty;

        prevUpdate ??= new() { Message = new() { Text = "/start", From = new() { Id = env.UserId } } };

        var succeeded = await router.TryHandle(prevUpdate);

        if (!succeeded)
        {
            await Back();
        }
    }

    public async Task MainMenu()
    {
        await defaultCommandService.SendMainMenu();
    }
}
