using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using FinanceTelegramBot.Base.Services;
using Telegram.Bot;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Base.Models;
using Microsoft.AspNetCore.Components.Routing;
using FinanceTelegramBot.Base;

namespace FinanceTelegramBot.Services;

public class KeywordTelegramService(
    CategoryService categoryService, 
    KeywordService keywordService,
    ITelegramBotClient bot,
    RouteEnvironment env,
    InlineKeyboardBuilder keyboardBuilder,
    StateService stateService,
    TelegramRouter router)
{
    private const int PageSize = 10;
    public async Task SendKeywordsList(long categoryId, int page)
    {
        var category = await categoryService.GetByIdAsync(categoryId);

        var text = $"🔍 Ключевые слова для категории\n*{category.Name}*:";

        foreach (var keyword in category.Keywords.Skip(10 * (page - 1)).Take(10))
        {
            keyboardBuilder.AppendCallbackData(keyword.Keyword, $"/Keyword/Settings/{keyword.Id}")
                .AppendLine();
        }

        var totalPages = (int)Math.Ceiling(category.Keywords.Count / (double)PageSize);

        keyboardBuilder.AppendPagination(
            page,
            totalPages,
            p => $"/keyword/getall/{categoryId}/{p}"
        ).AppendLine();

        keyboardBuilder
            .AppendCallbackData("+ Добавить ключевое слово", $"/keyword/initcreate/{categoryId}")
            .AppendLine()
            .AppendBackButton().AppendToMainMenuButton()
            .AppendLine();

        var markup = keyboardBuilder.Build();

        switch (env.Update.Type)
        {
            case UpdateType.Message:
                await bot.SendMessageWithKeyboard(env.UserId, text, markup, ParseMode.MarkdownV2);
                break;
            case UpdateType.CallbackQuery:
                await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, text, markup, ParseMode.MarkdownV2);
                break;
        }
    }

    public async Task InitCreate(long categoryId)
    {
        var text = "Введи имя нового ключевого слова 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = Create,
            Data = new() { { "categoryId", categoryId } }
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build());
    }

    public async Task SendSettings(long keywordId)
    {
        var keyword = await keywordService.GetByIdAsync(keywordId);

        if (keyword == null)
        {
            await SendErrorMessage(env.UserId, "Ключевое слово не найдено");
            return;
        }

        var text = $"🗂 Ключевое слово: *{keyword.Keyword}*";

        keyboardBuilder.AppendCallbackData("✏️ Переименовать", $"/Keyword/InitRename/{keywordId}")
            .AppendLine()
            .AppendCallbackData("🗑 Удалить ключевое слово", $"/Keyword/Delete/{keywordId}")
            .AppendLine()
        .AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, text, keyboardBuilder.Build(), parseMode: ParseMode.MarkdownV2);
    }

    public async Task Delete(long keywordId)
    {
        try
        {
            await keywordService.DeleteKeywordAsync(keywordId);
        }
        catch (BusinessException e)
        {
            await SendErrorMessage(env.UserId, e.Message);
            return;
        }

        await bot.SendMessage(env.UserId, "✅ Ключевое успешно удалена");

        env.Update.CallbackQuery!.Data = "/navigation/back";
        await router.TryHandle(new() { CallbackQuery = env.Update.CallbackQuery });
    }

    public async Task InitRename(long keywordId)
    {
        var keyword = await keywordService.GetByIdAsync(keywordId);
        if(keyword == null)
        {
            await SendErrorMessage(env.UserId, "Ключевое слово не удалось найти");
        }

        var text = $"Введи новое имя для ключевого слова *{keyword.Keyword}* 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = Rename,
            Data = new() { { "keywordId", keywordId } }
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build(), ParseMode.MarkdownV2);
    }

    private async Task Rename(Update update, UserState state, IServiceScope scope)
    {
        var userId = env.UserId;

        if (!state.Data.TryGetValue("keywordId", out var keywordIdObj) || keywordIdObj is not long keywordId)
        {
            await SendErrorMessage(userId, "Ошибка");
            return;
        }

        var keywordName = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(keywordName))
        {
            await SendErrorMessage(userId, "Имя ключевого слова не может быть пустым");
            return;
        }

        var scopedKeywordService = scope.ServiceProvider.GetRequiredService<KeywordService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedKeywordTelegramService = scope.ServiceProvider.GetRequiredService<KeywordTelegramService>();
        var scopedEnv = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();

        var keyword = await scopedKeywordService.GetByIdAsync(keywordId);
        if (keyword == null)
        {
            await SendErrorMessage(userId, "Не получилось найти ключевое слово");
            return;
        }

        keyword.Keyword = keywordName;

        scopedEnv.UserId = env.UserId;
        scopedEnv.Update = update;

        try
        {
            await scopedKeywordService.UpdateCategoryAsync(keyword);

            stateService.RemoveState(state);

            await bot.SendMessage(env.UserId, $"✅ Ключевое слово *\"{keyword.Keyword}\"* успешно переименованj", ParseMode.MarkdownV2);

            await scopedKeywordTelegramService.SendKeywordsList(keyword.CategoryId, 1);
        }
        catch (BusinessException ex)
        {
            await SendErrorMessage(userId, ex.Message);
        }
    }

    private async Task Create(Update update, UserState state, IServiceScope scope)
    {
        var userId = env.UserId;

        if (!state.Data.TryGetValue("categoryId", out var categoryIdObj) || categoryIdObj is not long categoryId)
        {
            await SendErrorMessage(userId, "Ошибка");
            return;
        }

        var keywordName = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(keywordName))
        {
            await SendErrorMessage(userId, "Ключевое слово не может быть пустым");
            return;
        }

        var keyword = new CategoryKeyword
        {
            Keyword = keywordName,
            CategoryId = categoryId
        };

        var scopedKeywordService = scope.ServiceProvider.GetRequiredService<KeywordService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedKeywordTelegramService = scope.ServiceProvider.GetRequiredService<KeywordTelegramService>();
        var scopedEnv = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();

        scopedEnv.UserId = env.UserId;
        scopedEnv.Update = update;

        try
        {
            await scopedKeywordService.CreateAsync(keyword);

            scopedStateService.RemoveState(state);

            await bot.SendMessage(env.UserId, $"✅ Ключевое слово *\"{keyword.Keyword}\"* успешно добавлено", ParseMode.MarkdownV2);

            await scopedKeywordTelegramService.SendKeywordsList(categoryId, 1);
        }
        catch (BusinessException ex)
        {
            await SendErrorMessage(userId, ex.Message);
        }
    }

    private async Task SendErrorMessage(long userId, string message)
    {
        await bot.SendMessage(userId, message);
    }
}
