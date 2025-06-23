using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Services;

namespace FinanceTelegramBot.Controllers;

public class DefaultController(
    UserService userService,
    DefaultCommandService defaultCommandService, 
    MoneyTransactionTelegramService transactionTelegramService,
    RouteEnvironment env,
    TelegramRouter router,
    ICallbackDataStore dataStore
    ) : ITelegramController
{
    public async Task Index()
    {
        if(env.Update.Type == UpdateType.Message)
        {
            if(env.Update.Message!.Photo != null)
            {
                if (!string.IsNullOrEmpty(env.Update.Message!.Caption))
                {
                    env.Update.Message!.Text = "/tr/createbyphoto/" + env.Update.Message!.Caption;
                }
                else
                {
                    env.Update.Message!.Text = "/tr/createbyphoto";
                }

                await router.TryHandle(env.Update);
                return;
            }

            var transactionParsed = transactionTelegramService.TryParseTransaction(env.Update.Message!.Text!, out var transaction);
            if (transactionParsed)
            {
                var guid = await dataStore.StoreDataAsync(transaction!);
                env.Update.Message.Text = $"/tr/cr/" + guid;
                await router.TryHandle(new() { Message = env.Update.Message });
                return;
            }
        }

        await defaultCommandService.SendDefaultMessage();
    }

    [TelegramRoute("/start")]
    [TelegramCommand("/start", "Начало работы с ботом")]
    public async Task Start()
    {
        var telegramUser = env.Update.CallbackQuery != null ? env.Update.CallbackQuery.From :
            env.Update.Message != null ? env.Update.Message.From : null;

        if(telegramUser != null && !await userService.UserExistsAsync(env.UserId))
        {
            var user = new User()
            {
                Id = telegramUser.Id,
                CreatedAt = DateTime.UtcNow,
                FirstName = telegramUser.FirstName,
                LastName = telegramUser.LastName,
                Username = telegramUser.Username
            };

            await userService.CreateAsync(user);
        }

        await defaultCommandService.SendMainMenu();
    }

    [TelegramRoute("/help")]
    [TelegramCommand("/help", "Список всех команд")]
    public async Task Help()
    {
        await defaultCommandService.SendCommandList();
    }

    [TelegramRoute("/[action]")]
    public void Empty()
    {

    }
}
