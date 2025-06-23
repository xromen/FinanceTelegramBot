using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using static System.Net.Mime.MediaTypeNames;

namespace FinanceTelegramBot.Services;

public class CategoryTelegramService(
    CategoryService categoryService, 
    InlineKeyboardBuilder keyboardBuilder,
    RouteEnvironment env,
    StateService stateService,
    TelegramRouter router,
    ITelegramBotClient bot)
{
    private const int PageSize = 10;

    public async Task SendCategoryList(TransactionType type, int page)
    {
        string text = type == TransactionType.Income ? "📋 Категории доходов" : "📋 Категории трат";

        var categories = await categoryService.GetAllCategoriesByUserIdAsync(env.UserId, type);
        var pagedCategories = categories.Skip(PageSize * (page - 1)).Take(PageSize);

        foreach (var category in pagedCategories)
        {
            keyboardBuilder.AppendCallbackData(category.Name, $"/Category/Settings/{category.Id}")
            .AppendLine();
        }

        var totalPages = (int)Math.Ceiling(categories.Count / (double)PageSize);

        keyboardBuilder.AppendPagination(page, totalPages, p => $"/Category/GetAll/{type}/{p}")
            .AppendLine();

        keyboardBuilder.AppendCallbackData("+ Добавить категорию", $"/Category/InitCreate/{type}")
            .AppendLine();

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton().AppendLine();

        var markup = keyboardBuilder.Build();

        switch (env.Update.Type)
        {
            case UpdateType.Message:
                await bot.SendMessageWithKeyboard(env.UserId, text, markup);
                break;
            case UpdateType.CallbackQuery:
                await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, text, markup);
                break;
        }
    }

    public async Task ChooseTransactionType()
    {
        var text = "📒Группа категорий:";
        var keyboard = keyboardBuilder
            .AppendCallbackData("➕ Доходы", $"/Category/GetAll/{TransactionType.Income.ToString()}")
            .AppendLine()
            .AppendCallbackData("➖ Траты", $"/Category/GetAll/{TransactionType.Expense.ToString()}")
            .AppendLine()
            .AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, text, keyboard.Build());
    }

    public async Task InitCreate(TransactionType type)
    {
        var text = "Введи имя новой категории 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = Create,
            Data = new() { { "type", type } }
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build());
    }

    public async Task SendSettings(long categoryId)
    {
        var category = await categoryService.GetByIdAsync(categoryId);

        if (category == null)
        {
            await SendErrorMessage(env.UserId, "Категория не найдена");
            return;
        }

        var text = $"🗂 Категория: *{category.Name}*";

        keyboardBuilder.AppendCallbackData("🔍 Ключевые слова", $"/Keyword/GetAll/{categoryId}")
            .AppendLine()
            .AppendCallbackData("✏️ Изменить имя категории", $"/Category/InitRename/{categoryId}")
            .AppendLine()
            .AppendCallbackData("🗑 Удалить категорию", $"/Category/Delete/{categoryId}")
            .AppendLine()
        .AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, text, keyboardBuilder.Build(), parseMode: ParseMode.MarkdownV2);
    }

    public async Task Delete(long categoryId)
    {
        try
        {
            await categoryService.DeleteCategoryAsync(categoryId);
        }
        catch(BusinessException e)
        {
            await SendErrorMessage(env.UserId, e.Message);
            return;
        }

        await bot.SendMessage(env.UserId, "✅ Категория успешно удалена");

        env.Update.CallbackQuery!.Data = "/navigation/back";
        await router.TryHandle(new() { CallbackQuery = env.Update.CallbackQuery });
    }

    public async Task InitRename(long categoryId)
    {
        var category = await categoryService.GetByIdAsync(categoryId);
        var text = $"Введи новое имя для категории *{category.Name}* 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = Rename,
            Data = new() { { "categoryId", categoryId } }
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build(), ParseMode.MarkdownV2);
    }

    private async Task Rename(Update update, UserState state, IServiceScope scope)
    {
        var userId = env.UserId;

        if (!state.Data.TryGetValue("categoryId", out var categoryIdObj) || categoryIdObj is not long categoryId)
        {
            await SendErrorMessage(userId, "Ошибка");
            return;
        }

        var categoryName = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            await SendErrorMessage(userId, "Имя категории не может быть пустым");
            return;
        }

        var scopedCategoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedCategoryTelegramService = scope.ServiceProvider.GetRequiredService<CategoryTelegramService>();
        var scopedEnv = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();

        var category = await scopedCategoryService.GetByIdAsync(categoryId);
        if(category == null)
        {
            await SendErrorMessage(userId, "Не получилось найти категорию");
            return;
        }

        category.Name = categoryName;

        scopedEnv.UserId = env.UserId;
        scopedEnv.Update = update;

        try
        {
            await scopedCategoryService.UpdateCategoryAsync(category);

            stateService.RemoveState(state);

            await bot.SendMessage(env.UserId, $"✅ Категория *\"{category.Name}\"* успешно переименована", ParseMode.MarkdownV2);

            await scopedCategoryTelegramService.SendCategoryList(category.Type, 1);
        }
        catch (BusinessException ex)
        {
            await SendErrorMessage(userId, ex.Message);
        }
    }

    private async Task Create(Update update, UserState state, IServiceScope scope)
    {
        var userId = env.UserId;

        if (!state.Data.TryGetValue("type", out var typeObj) || typeObj is not TransactionType type)
        {
            await SendErrorMessage(userId, "Ошибка");
            return;
        }

        var categoryName = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            await SendErrorMessage(userId, "Имя категории не может быть пустым");
            return;
        }

        var category = new Category
        {
            Name = categoryName,
            Type = type,
            UserId = userId
        };

        var scopedCategoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedCategoryTelegramService = scope.ServiceProvider.GetRequiredService<CategoryTelegramService>();
        var scopedEnv = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();

        scopedEnv.UserId = env.UserId;
        scopedEnv.Update = update;

        try
        {
            await categoryService.CreateAsync(category);

            stateService.RemoveState(state);

            await bot.SendMessage(env.UserId, $"✅ Категория *\"{category.Name}\"* успешно добавлена", ParseMode.MarkdownV2);

            await scopedCategoryTelegramService.SendCategoryList(type, 1);
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
