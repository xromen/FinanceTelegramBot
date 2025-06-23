using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Models;
using static System.Net.Mime.MediaTypeNames;

namespace FinanceTelegramBot.Services;

public class DefaultCommandService(
    ITelegramBotClient bot, 
    InlineKeyboardBuilder keyboardBuilder, 
    RouteEnvironment env,
    TelegramCommandRegistry commandRegistry
    )
{
    public async Task SendMainMenu()
    {
        keyboardBuilder.AppendCallbackData("🧮 Баланс", "/tr/getbalance").AppendLine();
        keyboardBuilder.AppendCallbackData("👨‍👩‍👦 Управление семьей", "/family/settings").AppendLine();
        keyboardBuilder.AppendCallbackData("🗂 Категории", "/category/getall").AppendLine();
        var markup = keyboardBuilder.Build();

        var text = """
            Привет! В данном меню ты можешь настроить категории трат или доходов, создать семью и управлять ее участниками или покинуть семью.

            Для добавления новой транзакции отправь сообщение с суммой и категорией. Транзакция добавится в текущий день. Для установки даты ты можешь написать день на второй строке.
            _Пример:
            1200000 зп
            05.01.2025
            Если ключевое слово или категория зп существует, то транзакция добавится автоматически, если нет то будет предложено создать новую или выбрать существующую._
            """;

        if (env.Update!.Type == UpdateType.CallbackQuery)
        {
            await bot.EditMessageText(env.UserId, env.Update.CallbackQuery.Message.Id, text, ParseMode.Markdown, replyMarkup: markup);
            return;
        }

        await bot.SendMessageWithKeyboard(env.UserId, text, markup, ParseMode.Markdown);
    }

    public async Task SendCommandList()
    {
        var commands = commandRegistry.GetTelegramCommands();

        var text = "📋 Список доступных команд:\n\n" +
                   string.Join("\n", commands.Select(c => $"/{c.Command} — {c.Description}"));

        await bot.SendMessage(env.UserId, text);
    }

    public async Task SendDefaultMessage()
    {
        await bot.SendMessage(env.UserId, "Не понял твоей команды. Используй /help для просмотра списка команд.");
    }
}
