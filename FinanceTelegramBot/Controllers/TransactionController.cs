using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Services;

namespace FinanceTelegramBot.Controllers;

public class TransactionController(
    CategoryService categoryService, 
    RouteEnvironment env,
    MoneyTransactionService transactionService,
    MoneyTransactionTelegramService transactionTelegramService,
    KeywordService keywordService,
    ITelegramBotClient bot) : ITelegramController
{
    [TelegramRoute("/tr/cr/{dto}/{categoryId:null}")]
    public async Task<bool> Create([FromDataStore] TransactionDto dto, long? categoryId)
    {
        Category? category = null;
        if (string.IsNullOrEmpty(dto.Keyword))
        {
            if (categoryId.HasValue)
            {
                category = await categoryService.GetByIdAsync(categoryId.Value);
            }
            else if(!await categoryService.AnyAsync(c => c.Type == TransactionType.OurExpense))
            {
                category = await categoryService.CreateAsync(new() 
                { 
                    Name = "Прочие траты", 
                    UserId = env.UserId, 
                    Type = TransactionType.OurExpense 
                });
            }
            else
            {
                category = (await categoryService.GetAllCategoriesByUserIdAsync(env.UserId, TransactionType.OurExpense)).First();
            }
        }
        else
        {
            category = await categoryService.GetCategoryByKeyword(env.UserId, dto.Keyword);
        }

        if(category == null && categoryId == null)
        {
            await transactionTelegramService.ChooseCategory(dto.Type, 1, dto);
            return true;
        }

        if(category == null && categoryId != null)
        {
            var keyword = await keywordService.CreateAsync(new() { CategoryId = categoryId.Value, Keyword = dto.Keyword });
            category = await categoryService.GetByIdAsync(categoryId.Value);
        }

        await transactionTelegramService.Create(dto, category!);

        return false;
    }

    [TelegramRoute("/tr/chcat/{dto}/{type}/{page:null}")]
    public async Task ChooseCategory([FromDataStore] TransactionDto dto, TransactionType type, int? page = 1)
    {
        await transactionTelegramService.ChooseCategory(type, page!.Value, dto);
    }

    [TelegramRoute("/tr/incrcat/{dto}/{type}")]
    public async Task<bool> InitCreateCategory([FromDataStore] TransactionDto dto, TransactionType type)
    {
        await transactionTelegramService.InitCreateCategory(dto, type);

        return true;
    }

    [TelegramRoute("/tr/createbyphoto/{keyword:null}")]
    public async Task CreateByPhoto(string? keyword)
    {
        if(env.Update.Message == null || env.Update.Message.Photo == null)
        {
            return;
        }

        await transactionTelegramService.InitCreateByPhoto(keyword);
    }

    [TelegramRoute("/tr/getbalance")]
    public async Task GetBalance()
    {
        await transactionTelegramService.SendCurrentBalance();
    }

    [TelegramRoute("/tr/ReportByCategory/{type}/{year:null}/{month:null}/{page:null}")]
    public async Task ReportByCategory(TransactionType type, int? year, int? month, int? page = 1)
    {
        await transactionTelegramService.ReportByCategory(type, year ?? DateTime.Now.Year, month ?? DateTime.Now.Month, page!.Value);
    }

    [TelegramRoute("/tr/AnnualStatistics")]
    public async Task AnnualStatistics()
    {
        await transactionTelegramService.SendAnnualStatistics();
    }

    [TelegramRoute("/tr/getall/{type:null}/{year:null}/{month:null}/{page:null}")]
    public async Task GetAll(TransactionType? type, int? year, int? month, int? page = 1)
    {
        await transactionTelegramService.SendAllByMonth(type, year ?? DateTime.Now.Year, month ?? DateTime.Now.Month, page.Value);
    }

    [TelegramRoute("/tr/ChooseMonth/{type:null}")]
    public async Task ChooseMonth(TransactionType? type)
    {
        await transactionTelegramService.SendChooseMonth(type);
    }

    [TelegramRoute("/tr/Edit/{transactionId}")]
    public async Task Edit(long transactionId)
    {
        await transactionTelegramService.EditTransaction(transactionId);
    }

    [TelegramRoute("/tr/amountedit/{transactionId}")]
    public async Task<bool> AmountEdit(long transactionId)
    {
        await transactionTelegramService.EditAmount(transactionId);

        return false;
    }

    [TelegramRoute("/tr/CategoryEdit/{transactionId}/{categoryId:null}/{page:null}")]
    public async Task<bool> CategoryEdit(long transactionId, long? categoryId, int? page = 1)
    {
        await transactionTelegramService.EditCategory(transactionId, categoryId, page!.Value);

        return false;
    }

    [TelegramRoute("/tr/dateedit/{transactionId}")]
    public async Task<bool> DateEdit(long transactionId)
    {
        await transactionTelegramService.EditDate(transactionId);

        return false;
    }

    [TelegramRoute("/tr/Delete/{transactionId}/{confirm:null}")]
    public async Task<bool> Delete(long transactionId, bool? confirm)
    {
        await transactionTelegramService.Delete(transactionId, confirm);

        return false;
    }

    [TelegramRoute("/tr/Export/{from:null}/{to:null}/{confirm:null}")]
    public async Task Export(DateTime? from, DateTime? to, bool? confirm)
    {
        await transactionTelegramService.Export(from, to, confirm);
    }

    [TelegramRoute("/tr/ChooseDatesForExport")]
    public async Task ChooseDatesForExport()
    {
        await transactionTelegramService.ChooseDatesForExport();
    }
}
