using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Models.ProverkaCheka;

namespace FinanceTelegramBot.Services;

public class MoneyTransactionTelegramService(
    CategoryService categoryService, 
    MoneyTransactionService transactionService,
    RouteEnvironment env,
    InlineKeyboardBuilder keyboardBuilder,
    StateService stateService,
    ProverkaChekaApi proverkaChekaApi,
    ICallbackDataStore dataStore,
    ITelegramBotClient bot)
{
    private const string TransactionCreatePattern = "^(?:(?<amount>\\d+(?:[.,]\\d+)?)\\s*(?<keyword>[^\\d\\n]+)?|(?<keyword>[^\\d\\n]+)?\\s*(?<amount>\\d+(?:[.,]\\d+)?))\\s*\\n?(?<date>\\d{2}\\.\\d{2}\\.\\d{4})?";
    private const int PageSize = 10;

    public bool TryParseTransaction(string text, out TransactionDto? transaction)
    {
        transaction = null;

        var match = Regex.Match(text, TransactionCreatePattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return false;
        }

        TransactionDto? result = new();

        if (match.Groups["keyword"].Success)
        {
            result.Keyword = match.Groups["keyword"].Value.Trim();
        }
        //else
        //{
        //    return false;
        //}

        if (match.Groups["amount"].Success && decimal.TryParse(match.Groups["amount"].Value.Trim(), out var amount))
        {
            result.Amount = amount;
        }
        else
        {
            return false;
        }

        DateOnly date = default;

        if (match.Groups["date"].Success)
        {
            if(!DateOnly.TryParse(match.Groups["date"].Value, out date))
            {
                return false;
            }
        }
        else
        {
            date = DateOnly.FromDateTime(DateTime.Now);
        }

        result.Date = date;

        transaction = result;

        return true;
    }

    public async Task ChooseCategory(TransactionType type, int page, TransactionDto dto)
    {
        dto.Type = type;
        var dtoGuid = await dataStore.StoreDataAsync(dto);

        var text = $"""
            🗂️ К какой категории относится "*{dto.Keyword}*"?

            _Ты можешь ассоциировать ключевое слово с существующей категорией или создать новую.

            Выбери тип категории (траты / доходы) и имя._👇🏼
            """;

        var expenseString = type == TransactionType.Expense ? "🔴 Траты" : "⚪️ Траты";
        var incomeString = type == TransactionType.Income ? "🟢 Доходы" : "⚪️ Доходы";

        var categories = await categoryService.GetAllCategoriesByUserIdAsync(env.UserId, type);
        var categoriesPaginated = categories.Skip(PageSize * (page - 1)).Take(PageSize);

        keyboardBuilder.AppendCallbackData(expenseString, $"/tr/chcat/{dtoGuid}/{TransactionType.Expense}/{page}");
        keyboardBuilder.AppendCallbackData(incomeString, $"/tr/chcat/{dtoGuid}/{TransactionType.Income}/{page}");
        keyboardBuilder.AppendLine();

        foreach(var category in categoriesPaginated)
        {
            keyboardBuilder.AppendCallbackData(category.Name, $"/tr/cr/{dtoGuid}/{category.Id}")
                .AppendLine();
        }

        var totalPages = (int)Math.Ceiling(categories.Count / (double)PageSize);

        keyboardBuilder.AppendPagination(page, totalPages, (p) => $"/tr/chcat/{dtoGuid}/{type}/{p}")
            .AppendLine();

        keyboardBuilder.AppendCallbackData("+ Добавить категорию", $"/tr/incrcat/{dtoGuid}/{type}")
            .AppendLine();

        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/empty");

        switch (env.Update.Type)
        {
            case UpdateType.Message:
                await bot.SendMessageWithKeyboard(env.UserId, text, keyboardBuilder.Build(), ParseMode.Markdown);
                break;
            case UpdateType.CallbackQuery:
                await bot.EditMessageText(env.UserId, env.Update.CallbackQuery!.Message!.Id, text, parseMode: ParseMode.Markdown, replyMarkup: keyboardBuilder.Build());
                break;
        }

    }

    public async Task Create(TransactionDto dto, Category category)
    {
        if(!string.IsNullOrEmpty(dto.CheckQrCodeRaw) && category.Type == TransactionType.Income)
        {
            await bot.SendMessage(env.UserId, "Нельзя добавить данные чека в категорию дохода");
            return;
        }

        var transaction = new MoneyTransaction()
        {
            Amount = dto.Amount,
            CategoryId = category!.Id,
            Date = dto.Date,
            UserId = env.UserId,
            CheckQrCodeRaw = dto.CheckQrCodeRaw,
            Items = dto.Items ?? new()
        };

        transaction = await transactionService.CreateAsync(transaction);
        var description = GetDescription(transaction);

        keyboardBuilder.AppendToMainMenuButton();

        switch (env.Update.Type)
        {
            case UpdateType.Message:
                await bot.SendMessageWithKeyboard(env.UserId, description, keyboardBuilder.Build(), ParseMode.Markdown);
                break;
            case UpdateType.CallbackQuery:
                await bot.EditMessageText(env.UserId, env.Update.CallbackQuery!.Message!.Id, description, ParseMode.Markdown, replyMarkup: keyboardBuilder.Build());
                break;
        }
    }

    public async Task InitCreateCategory(TransactionDto dto, TransactionType type)
    {
        var text = "Введи имя новой категории 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = CreateCategory,
            Data = new() { { "type", type }, { "dto", dto } }
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build());
    }

    public async Task InitCreateByPhoto(string? keyword)
    {
        await using var stream = new MemoryStream();
        var tgFile = await bot.GetInfoAndDownloadFile(env.Update.Message!.Photo!.Last().FileId, stream);
        stream.Position = 0;

        var statusMessage = await bot.SendMessage(env.UserId, "Отправка изображения на сайт ProverkaChekov...");
        var checkData = await TryGetCheckDataByPhoto(stream, Path.GetFileName(tgFile.FilePath));

        if (checkData.Code != 1)
        {
            await bot.EditMessageText(env.UserId, statusMessage.Id, "Ошибка во время получения данных");
            return;
        }
        await bot.EditMessageText(env.UserId, statusMessage.Id, "✅ Данные чека успешно получены! Выберете категорию");

        var jsonData = checkData.Data.Json!;

        var dto = new TransactionDto()
        {
            Keyword = keyword,
            Amount = jsonData.TotalSum / 100,
            Date = DateOnly.FromDateTime(jsonData.DateTime),
            Type = TransactionType.Expense,
            CheckQrCodeRaw = checkData.Request.Qrraw,
            Items = jsonData.Items.Select(item => new PurchaseItem
            {
                Name = item.Name,
                Price = item.Price / 100,
                Quantity = item.Quantity
            }).ToList()
        };

        if (!string.IsNullOrEmpty(keyword))
        {
            var category = await categoryService.GetCategoryByKeyword(env.UserId, keyword);
            if(category != null)
            {
                await Create(dto, category);
                return;
            }
        }

        await ChooseCategory(dto.Type, 1, dto);

        //await bot.EditMessageText(statusMessage.Chat.Id, statusMessage.Id, "Ошибка при обработке изображения. Пытаюсь отсканировать QR-код...");

        //stream.Position = 0;
        //checkData = await TryGetCheckDataByQrCode(bot, stream, statusMessage);

        //if (checkData.Code == 1)
        //    await OnSuccessfulCheckData(bot, message, userId, checkData, statusMessage, editMessage);
    }

    private async Task<GetCheckResponse> TryGetCheckDataByPhoto(Stream stream, string fileName)
    {
        try
        {
            return await proverkaChekaApi.GetCheckDataByPhoto(stream, fileName) ?? new() { Code = 5 };
        }
        catch (Exception ex)
        {
            //logger.LogError(ex, "Ошибка при вызове ProverkaChekov по фото");
            return new() { Code = 5 };
        }
    }

    private async Task CreateCategory(Update update, UserState state, IServiceScope scope)
    {
        var userId = env.UserId;

        if (!state.Data.TryGetValue("type", out var typeObj) || typeObj is not TransactionType type)
        {
            await bot.SendMessage(userId, "Ошибка");
            return;
        }

        if (!state.Data.TryGetValue("dto", out var dtoObj) || dtoObj is not TransactionDto dto)
        {
            await bot.SendMessage(userId, "Ошибка");
            return;
        }

        var categoryName = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            await bot.SendMessage(userId, "Имя категории не может быть пустым");
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
        var scopedTransactionTelegramService = scope.ServiceProvider.GetRequiredService<MoneyTransactionTelegramService>();
        var scopedEnv = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();

        scopedEnv.UserId = env.UserId;
        scopedEnv.Update = update;

        try
        {
            await scopedCategoryService.CreateAsync(category);

            await scopedTransactionTelegramService.Create(dto, category);

            scopedStateService.RemoveState(state);
        }
        catch (BusinessException ex)
        {
            await bot.SendMessage(userId, ex.Message);
        }
    }

    private string GetDescription(MoneyTransaction transaction)
    {
        string sign = transaction.Category.Type == TransactionType.Expense || transaction.Category.Type == TransactionType.OurExpense ? "-" : "";
        string amount = sign + transaction.Amount.ToString("C", new CultureInfo("ru-RU"));

        return $"""
        ✅ Транзакция успешно добавлена:
        💲 Сумма: *{amount}*
        🗂 Категория: *{transaction.Category.Name}*
        🗓️ Дата: *{transaction.Date:dd.MM.yyyy}*
        """;
    }
}
