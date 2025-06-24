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
using System.Text;
using System.Transactions;
using Microsoft.AspNetCore.Routing.Template;
using OfficeOpenXml;
using System.IO;
using Npgsql.Replication.PgOutput.Messages;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceTelegramBot.Services;

public class MoneyTransactionTelegramService(
    CategoryService categoryService,
    MoneyTransactionService transactionService,
    RouteEnvironment env,
    InlineKeyboardBuilder keyboardBuilder,
    StateService stateService,
    ProverkaChekaApi proverkaChekaApi,
    TelegramRouter router,
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
            if (!DateOnly.TryParse(match.Groups["date"].Value, out date))
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

        foreach (var category in categoriesPaginated)
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
        if (!string.IsNullOrEmpty(dto.CheckQrCodeRaw) && category.Type == TransactionType.Income)
        {
            await bot.SendMessage(env.UserId, "Нельзя добавить данные чека в категорию дохода");
            return;
        }

        var checkExists = await transactionService.AnyAsync(c => c.CheckQrCodeRaw == dto.CheckQrCodeRaw && dto.CheckQrCodeRaw != null);

        if (checkExists)
        {
            await bot.SendMessage(env.UserId, "Данный чек уже добавлен");
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
        var description = await GetDescription(transaction);

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
            if (category != null)
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

    public async Task SendCurrentBalance()
    {
        var today = DateTime.Now.Date;
        var balance = await transactionService.GetMonthBalance(env.UserId, today.Year, today.Month);

        var monthTransactions = await transactionService
            .GetAllTransactionsByUserIdAsync(env.UserId, c => c.Date.Year == today.Year && c.Date.Month == today.Month);

        var expenseAmount = monthTransactions.Where(c => c.Category.Type != TransactionType.Income).Select(c => c.Amount).Sum();
        var incomeAmount = monthTransactions.Where(c => c.Category.Type == TransactionType.Income).Select(c => c.Amount).Sum();

        var percent = incomeAmount == 0 ? 0 : (int)Math.Round(100 - (expenseAmount / incomeAmount * 100));
        var greenSquareCount = (int)Math.Round(percent / 10.0);

        var greenSquares = string.Concat(Enumerable.Repeat("🟩", greenSquareCount));
        var whiteSquares = string.Concat(Enumerable.Repeat("⬜️", 10 - greenSquareCount));

        var sign = balance > 0 ? "+" : "";

        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"🧮 <b><u>Баланс за {today:MMMM}</u></b>\n");
        sb.AppendLine($"📈 <b>{sign}{balance.ToString("C0", new CultureInfo("ru-RU"))}</b>\n");
        sb.AppendLine($"➕ {incomeAmount.ToString("C0", new CultureInfo("ru-RU"))}");
        sb.AppendLine($"➖ {expenseAmount.ToString("C0", new CultureInfo("ru-RU"))}\n");
        sb.AppendLine($"Остаток от дохода:");
        sb.AppendLine($"{greenSquares}{whiteSquares}{percent}%");

        keyboardBuilder.AppendCallbackData("🗂 Отчет по категориям", $"/Tr/ReportByCategory/{TransactionType.Expense}").AppendLine();
        keyboardBuilder.AppendCallbackData("📊 Статистика", "/Tr/AnnualStatistics").AppendLine();
        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, sb.ToString(), keyboardBuilder.Build(), ParseMode.Html);
    }

    public async Task SendAnnualStatistics()
    {
        var today = DateTime.Now.Date;

        var transactions = await transactionService.GetAllTransactionsByUserIdAsync(env.UserId, c => c.Date.Year == today.Year);
        var grouppedByMonth = transactions.GroupBy(c => c.Date.Month);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("📊 <b><u>Статистика</u></b>\n");

        foreach (var monthGroup in grouppedByMonth)
        {
            var amount = monthGroup.Select(c => c.Category.Type == TransactionType.Income ? c.Amount : -c.Amount).Sum();
            var expenseAmount = monthGroup.Where(c => c.Category.Type != TransactionType.Income).Select(c => c.Amount).Sum();
            var incomeAmount = monthGroup.Where(c => c.Category.Type == TransactionType.Income).Select(c => c.Amount).Sum();
            var percent = incomeAmount == 0 ? 0 : (int)Math.Round(100 - (expenseAmount / incomeAmount * 100));

            var sign = amount > 0 ? "+" : "";
            var sircle = amount > 0 ? "🟢" : "🔴";

            sb.AppendLine($"🗓️ <i>{new DateTime(today.Year, monthGroup.Key, 1):MMMM}</i>");
            sb.AppendLine($"{sircle} <b>{sign}{amount:C0}</b> ({percent}%)");
            sb.AppendLine($"➕ {incomeAmount:C0} | ➖ {-expenseAmount:C0}\n");
        }

        keyboardBuilder.AppendCallbackData("📤 Экспорт данных", "/tr/export").AppendLine();
        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, sb.ToString(), keyboardBuilder.Build(), ParseMode.Html);
    }

    public async Task ReportByCategory(TransactionType type, int year, int month, int page)
    {
        var date = new DateTime(year, month, 1);
        var firstDay = new DateOnly(date.Year, date.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var text = $"""
            🗂️ *Отчет по категориям
            🗓️ за {firstDay:dd} - {lastDay:dd} {date:MMM} {date.Year}*
            """;

        var expenseSircle = type == TransactionType.Expense ? "🔴" : "⚪️";
        var expenseData = type == TransactionType.Expense ? "/empty" : $"/tr/ReportByCategory/{TransactionType.Expense}/{year}/{month}/{page}";

        var incomeSircle = type == TransactionType.Income ? "🟢" : "⚪️";
        var incomeData = type == TransactionType.Income ? "/empty" : $"/tr/ReportByCategory/{TransactionType.Income}/{year}/{month}/{page}";

        keyboardBuilder.AppendCallbackData($"{expenseSircle} Траты", expenseData);
        keyboardBuilder.AppendCallbackData($"{incomeSircle} Доходы", incomeData);
        keyboardBuilder.AppendLine();

        var transactions = await transactionService
            .GetAllTransactionsByUserIdAsync(env.UserId, c => c.Date.Year == date.Year && c.Date.Month == date.Month && c.Category.Type == type);

        var grouppedTransactions = transactions
            .OrderByDescending(x => x.Amount)
            .GroupBy(c => c.Category.Name)
            .Select(c => new
            {
                CategoryName = c.Key,
                Amount = c.Sum(cc => cc.Amount)
            });

        var paginatedCategories = grouppedTransactions.Skip(PageSize * (page - 1)).Take(PageSize);

        foreach (var category in paginatedCategories)
        {
            keyboardBuilder.AppendCallbackData($"{category.CategoryName} : {category.Amount:C0}", "/empty").AppendLine();
        }

        var totalPages = (int)Math.Ceiling(grouppedTransactions.Count() / (double)PageSize);

        keyboardBuilder.AppendPagination(page, totalPages, p => $"/tr/ReportByCategory/{type}/{year}/{month}/{p}");

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, text, keyboardBuilder.Build(), ParseMode.Markdown);
    }

    public async Task SendAllByMonth(TransactionType? type, int year, int month, int page)
    {
        string monthName = new DateTime(year, month, 1).ToString("MMMM");
        var transactions = await transactionService
            .GetAllTransactionsByUserIdAsync(env.UserId, c => c.Date.Month == month && c.Date.Year == year && (c.Category.Type == type || type == null));

        string allCircle = type == null ? "🟠" : "⚪️";
        string incomeCircle = type == TransactionType.Income ? "🟢" : "⚪️";
        string expenseCircle = type != null && type != TransactionType.Income ? "🔴" : "⚪️";

        keyboardBuilder.AppendCallbackData(monthName, $"/tr/ChooseMonth/{type}").AppendLine();
        keyboardBuilder.AppendCallbackData($"{allCircle} Все", $"/tr/getall//{year}/{month}");
        keyboardBuilder.AppendCallbackData($"{expenseCircle} Траты", $"/tr/getall/{TransactionType.Expense}/{year}/{month}");
        keyboardBuilder.AppendCallbackData($"{incomeCircle} Доходы", $"/tr/getall/{TransactionType.Income}/{year}/{month}");
        keyboardBuilder.AppendLine();

        var paginatedTransactions = transactions.OrderByDescending(c => c.Date).Skip(PageSize * (page - 1)).Take(PageSize);

        foreach (MoneyTransaction transaction in paginatedTransactions)
        {
            var sign = transaction.Category.Type == TransactionType.Income ? "+" : "-";

            keyboardBuilder.AppendCallbackData($"{sign}{transaction.Amount:C0} {transaction.Category.Name} 🗓 {transaction.Date.Day}", $"/tr/edit/{transaction.Id}")
                .AppendLine();
        }

        var totalPages = (int)Math.Ceiling(transactions.Count / (double)PageSize);

        //[TelegramRoute("/tr/getall/{type:null}/{year:null}/{month:null}/{page:null}")]
        var paginatedData = $"/tr/getall/{type}/{year}/{month}/";

        keyboardBuilder.AppendPagination(page, totalPages, p => paginatedData + p);
        keyboardBuilder.AppendCallbackData("🗂 Отчет по категориям", $"/Tr/ReportByCategory/{TransactionType.Expense}/{year}/{month}").AppendLine();
        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "🧾 <b>Журнал операций</b>", keyboardBuilder.Build(), ParseMode.Html);
    }
    public async Task SendChooseMonth(TransactionType? type)
    {
        var dates = await transactionService.GetTransactionsDate(env.UserId);
        var monthes = dates.Select(c => new { c.Month, c.Year, Str = c.ToString("MMMM yyyy") }).DistinctBy(c => c.Str);

        foreach (var month in monthes)
        {
            keyboardBuilder.AppendCallbackData(month.Str, $"/tr/getall/{type}/{month.Year}/{month.Month}/1").AppendLine();
        }

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "🗓️ <b>Выбери месяц</b>", keyboardBuilder.Build(), ParseMode.Html);
    }

    public async Task EditTransaction(long transactionId)
    {
        var text = """
            👀 Дитэйлс
            (для правки, нажми на то, что нужно изменить)
            """;

        var transaction = await transactionService.GetByIdAsync(transactionId);

        keyboardBuilder.AppendCallbackData(transaction!.Amount.ToString("C0"), $"/tr/amountedit/{transactionId}").AppendLine();
        keyboardBuilder.AppendCallbackData(transaction.Category.Name, $"/tr/categoryedit/{transactionId}").AppendLine();
        keyboardBuilder.AppendCallbackData(transaction.Date.ToString("dd MMM yyyy") + "г.", $"/tr/dateedit/{transactionId}").AppendLine();
        keyboardBuilder.AppendCallbackData("🗑 Удалить", $"/tr/delete/{transactionId}").AppendLine();
        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery?.Message, text, keyboardBuilder.Build());
    }

    public async Task EditAmount(long transactionId)
    {
        var transaction = await transactionService.GetByIdAsync(transactionId);

        var text = $"Чтобы изменить сумму операции с <b>{transaction.Amount:C0}</b>, введи новое значение 👇🏼";

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = EditAmount,
            Data = new() { { "transactionId", transactionId } }
        });

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, text, keyboardBuilder.Build(), ParseMode.Html);
    }

    public async Task EditCategory(long transactionId, long? categoryId, int page)
    {
        var transaction = await transactionService.GetByIdAsync(transactionId);

        if (categoryId == null)
        {
            var categories = await categoryService.GetAllCategoriesByUserIdAsync(env.UserId, transaction.Category.Type);

            var paginatedCategories = categories.Skip(PageSize * (page - 1)).Take(PageSize);

            foreach (var category in paginatedCategories)
            {
                keyboardBuilder.AppendCallbackData(category.Name, $"/tr/categoryedit/{transactionId}/{category.Id}").AppendLine();
            }

            var totalPages = (int)Math.Ceiling(categories.Count / (double)PageSize);

            keyboardBuilder.AppendPagination(page, totalPages, p => $"/tr/categoryedit/{transactionId}//{page}");

            keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "Выбери новую категорию для операции", keyboardBuilder.Build());
        }
        else
        {
            transaction.CategoryId = categoryId.Value;

            await transactionService.UpdateAsync(transaction);

            await EditTransaction(transactionId);
        }
    }

    public async Task EditDate(long transactionId)
    {
        var text = "Введите новую дату транзакции 👇🏼";

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = EditDate,
            Data = new() { { "transactionId", transactionId } }
        });

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, text, keyboardBuilder.Build());
    }

    public async Task Delete(long transactionId, bool? confirm)
    {
        if (confirm == null)
        {
            var text = "❓ Действительно удалить операцию?";

            keyboardBuilder.AppendCallbackData("🗑 Удалить", $"/tr/delete/{transactionId}/true");
            keyboardBuilder.AppendLine();
            keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, text, keyboardBuilder.Build());
        }
        else if (confirm.Value)
        {
            await transactionService.DeleteTransactionAsyns(transactionId);

            await bot.DeleteMessage(env.UserId, env.Update.CallbackQuery.Message.Id);

            await bot.SendMessage(env.UserId, "🗑️ Операция успешно удалена!");

            env.Update.CallbackQuery.Data = "/navigation/back";

            await router.TryHandle(env.Update);
        }
    }

    public async Task Export(DateTime? from, DateTime? to, bool? confirm)
    {
        if (from == null || to == null)
        {
            var text = "📤 Выбери период для экспорта данных в .xlsx файл:";

            var dates = await transactionService.GetTransactionsDate(env.UserId);
            var monthes = dates.Select(c => new { c.Month, c.Year, Str = c.ToString("MMMM yyyy") }).DistinctBy(c => c.Str);

            foreach (var date in monthes)
            {
                var fromDt = new DateTime(date.Year, date.Month, 1);
                var toDt = fromDt.AddMonths(1).AddDays(-1);

                keyboardBuilder.AppendCallbackData(date.Str, $"/tr/export/{fromDt:dd.MM.yyyy}/{toDt:dd.MM.yyyy}").AppendLine();
            }

            keyboardBuilder.AppendCallbackData("Указать произвольные даты", "/tr/ChooseDatesForExport").AppendLine();
            keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery?.Message, text, keyboardBuilder.Build());
        }
        else if(!confirm.HasValue)
        {
            var text = $"""
                📤 <b>Подтверждение экспорта</b>
                {from:dd MMM yyyy г.} - {to:dd MMM yyyy г.}
                """;

            keyboardBuilder.AppendCallbackData("💾 Экспортировать", $"/tr/export/{from:dd.MM.yyyy}/{to:dd.MM.yyyy}/true").AppendLine();
            keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery?.Message, text, keyboardBuilder.Build(), ParseMode.Html);
        }
        else if(confirm.Value)
        {
            var fromDo = DateOnly.FromDateTime(from.Value);
            var toDo = DateOnly.FromDateTime(to.Value);

            var transactions = await transactionService.GetAllTransactionsByUserIdAsync(env.UserId, c => c.Date >= fromDo && c.Date <= toDo);

            var excel = ExportToExcel(transactions, from.Value, to.Value);

            var inputFile = InputFile.FromStream(excel, $"{from:dd.MM.yyyy} - {to:dd.MM.yyyy}.xlsx");

            await bot.SendDocument(env.UserId, inputFile, $"✔️ Отчет за период: {from:dd MMM yyyy г.} - {to:dd MMM yyyy г.} готов!");
        }
    }

    public async Task ChooseDatesForExport()
    {
        var text = """
            📅 <b>Укажите даты для эксопрта</b>
            <i>Формат должен быть следуюший dd.mm.yyyy-dd.mm.yyyy 
            Например: 01.01.2025-31.01.2025</i>
        """;
        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = ChooseDatesForExport
        });

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, text, keyboardBuilder.Build(), ParseMode.Html);
    }

    private async Task ChooseDatesForExport(Update update, UserState state, IServiceScope scope)
    {
        var splitted = update.Message.Text.Split('-');

        if(splitted.Count() != 2 || !DateTime.TryParse(splitted[0], out var from) || !DateTime.TryParse(splitted[1], out var to))
        {
            await bot.SendMessage(env.UserId, "Ошибка. Формат дат должен быть dd.mm.yyyy-dd.mm.yyyy");
            return;
        }

        var scopedTransactionTelegramService = scope.ServiceProvider.GetRequiredService<MoneyTransactionTelegramService>();

        await scopedTransactionTelegramService.Export(from, to, null);
    }

    private Stream ExportToExcel(IEnumerable<MoneyTransaction> transactions, DateTime from, DateTime to)
    {
        ExcelPackage ep = new();
        var transactionsEws = ep.Workbook.Worksheets.Add($"{from:dd.MM.yyyy} - {to:dd.MM.yyyy}");
        var purchaseItemsEws = ep.Workbook.Worksheets.Add($"Данные чеков за {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");

        transactionsEws.Cells[1, 1].Value = "Id";
        transactionsEws.Cells[1, 2].Value = "Сумма";
        transactionsEws.Cells[1, 3].Value = "Дата";
        transactionsEws.Cells[1, 4].Value = "Категория";
        transactionsEws.Cells[1, 5].Value = "Пользователь";
        transactionsEws.Columns[3].Style.Numberformat.Format = "dd.MM.yyyy";

        purchaseItemsEws.Cells[1, 1].Value = "Цена";
        purchaseItemsEws.Cells[1, 2].Value = "Количество";
        purchaseItemsEws.Cells[1, 3].Value = "Наименование";
        purchaseItemsEws.Cells[1, 4].Value = "Дата";
        purchaseItemsEws.Cells[1, 5].Value = "Категория";
        purchaseItemsEws.Cells[1, 6].Value = "Пользователь";
        purchaseItemsEws.Cells[1, 7].Value = "Id транзакции";
        purchaseItemsEws.Columns[4].Style.Numberformat.Format = "dd.MM.yyyy";

        int transactionsRow = 2;
        int purchaseItemsRow = 2;

        foreach (var transaction in transactions)
        {
            transactionsEws.Cells[transactionsRow, 1].Value = transaction.Id;
            transactionsEws.Cells[transactionsRow, 2].Value = transaction.Category.Type == TransactionType.Income ? transaction.Amount : -transaction.Amount;
            transactionsEws.Cells[transactionsRow, 3].Value = transaction.Date;
            transactionsEws.Cells[transactionsRow, 4].Value = transaction.Category.Name;
            transactionsEws.Cells[transactionsRow, 5].Value = transaction.User.FirstName;

            transactionsRow++;

            foreach (var item in transaction.Items)
            {
                purchaseItemsEws.Cells[purchaseItemsRow, 1].Value = item.Price;
                purchaseItemsEws.Cells[purchaseItemsRow, 2].Value = item.Quantity;
                purchaseItemsEws.Cells[purchaseItemsRow, 3].Value = item.Name;
                purchaseItemsEws.Cells[purchaseItemsRow, 4].Value = transaction.Date;
                purchaseItemsEws.Cells[purchaseItemsRow, 5].Value = transaction.Category.Name;
                purchaseItemsEws.Cells[purchaseItemsRow, 6].Value = transaction.User.FirstName;
                purchaseItemsEws.Cells[purchaseItemsRow, 7].Value = transaction.Id;

                purchaseItemsRow++;
            }
        }

        AutoFitColumns(transactionsEws, 1, 5);
        AutoFitColumns(purchaseItemsEws, 1, 7);

        var stream = new MemoryStream();
        ep.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }

    private void AutoFitColumns(ExcelWorksheet ews, int from, int to)
    {
        for(int i = from; i <= to; i++)
        {
            ews.Column(i).AutoFit();
        }
    }

    private async Task EditDate(Update update, UserState state, IServiceScope scope)
    {
        if (!state.Data.TryGetValue("transactionId", out var transactionIdObj) || transactionIdObj is not long transactionId)
        {
            await bot.SendMessage(env.UserId, "Ошибка");
            return;
        }

        if (!DateOnly.TryParse(update.Message.Text, out var date))
        {
            await bot.SendMessage(env.UserId, "Ошибка");
            return;
        }

        var scopedMoneyTransactionService = scope.ServiceProvider.GetRequiredService<MoneyTransactionService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedTransactionTelegramService = scope.ServiceProvider.GetRequiredService<MoneyTransactionTelegramService>();
        var scopedbot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        var transaction = await scopedMoneyTransactionService.GetByIdAsync(transactionId);

        if (transaction == null)
        {
            await bot.SendMessage(env.UserId, "Ошибка. Транзакции не существует");
            scopedStateService.RemoveState(state);
            return;
        }

        transaction.Date = date;

        try
        {
            await scopedMoneyTransactionService.UpdateAsync(transaction);

            await scopedbot.SendMessage(env.UserId, "✅ Дата операции изменена!");

            await scopedTransactionTelegramService.EditTransaction(transactionId);

            scopedStateService.RemoveState(state);
        }
        catch (BusinessException ex)
        {
            await bot.SendMessage(env.UserId, ex.Message);
        }
    }

    private async Task EditAmount(Update update, UserState state, IServiceScope scope)
    {
        if (!state.Data.TryGetValue("transactionId", out var transactionIdObj) || transactionIdObj is not long transactionId)
        {
            await bot.SendMessage(env.UserId, "Ошибка");
            return;
        }

        if (!decimal.TryParse(update.Message.Text, out var amount) || amount < 0)
        {
            await bot.SendMessage(env.UserId, "Ошибка. Сумма операции должна быть больше 0");
            return;
        }

        var scopedMoneyTransactionService = scope.ServiceProvider.GetRequiredService<MoneyTransactionService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedTransactionTelegramService = scope.ServiceProvider.GetRequiredService<MoneyTransactionTelegramService>();
        var scopedbot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        var transaction = await scopedMoneyTransactionService.GetByIdAsync(transactionId);

        if (transaction == null)
        {
            await bot.SendMessage(env.UserId, "Ошибка. Транзакции не существует");
            scopedStateService.RemoveState(state);
            return;
        }

        transaction.Amount = amount;

        try
        {
            await scopedMoneyTransactionService.UpdateAsync(transaction);

            await scopedbot.SendMessage(env.UserId, "✅ Сумма операции изменена!");

            await scopedTransactionTelegramService.EditTransaction(transactionId);

            scopedStateService.RemoveState(state);
        }
        catch (BusinessException ex)
        {
            await bot.SendMessage(env.UserId, ex.Message);
        }
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

    private async Task<string> GetDescription(MoneyTransaction transaction)
    {
        string sign = transaction.Category.Type == TransactionType.Expense || transaction.Category.Type == TransactionType.OurExpense ? "-" : "";
        string amountString = sign + transaction.Amount.ToString("C0");

        var today = DateTime.Now;

        decimal balance = await transactionService.GetMonthBalance(transaction.UserId, today.Year, today.Month);
        var balanceSign = balance > 0 ? "+" : "";

        return $"""
        ✅ Транзакция успешно добавлена:
        *{amountString}*
        *{transaction.Category.Name}*
        🗓️ *{transaction.Date:dd.MM.yyyy}*
        📈 {balanceSign}{balance:C0}
        """;
    }
}
