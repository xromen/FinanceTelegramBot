using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Base.Services;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FinanceTelegramBot;

public class BotBackgroundService(
    ITelegramBotClient botClient,
    TelegramRouter router,
    IServiceProvider serviceProvider,
    TelegramCommandRegistry commandRegistry,
    ILogger<BotBackgroundService> logger
    ) : BackgroundService
{
    private readonly ReceiverOptions _receiverOptions = new()
    {
        AllowedUpdates =
        [
            UpdateType.Message,
            UpdateType.CallbackQuery,
        ],
        DropPendingUpdates = false,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await botClient.DeleteMyCommands();

        var commands = commandRegistry.GetTelegramCommands();

        await botClient.SetMyCommands(commands, cancellationToken: stoppingToken);

        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("Bot @{Username} started", me.Username);

        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, _receiverOptions, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var start = DateTime.Now;
        var data = string.Empty;

        switch (update.Type)
        {
            case UpdateType.Message:
                data = update.Message!.Text;

                break;
            case UpdateType.CallbackQuery:
                data = update.CallbackQuery!.Data;
                break;
        }

        await router.TryHandle(update);

        logger.LogDebug($"Route {data} Ellapsed {(DateTime.Now - start).TotalMilliseconds}ms");

        if (update.Type == UpdateType.CallbackQuery)
        {
            await bot.AnswerCallbackQuery(update.CallbackQuery!.Id, cancellationToken: cancellationToken);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ApiRequestException api:
                logger.LogError(api, "Telegram API Error: [{ErrorCode}] {Message}", api.ErrorCode, api.Message);
                break;

            default:
                logger.LogError(exception, "Internal Error: ");
                break;
        }

        return Task.CompletedTask;
    }
}
