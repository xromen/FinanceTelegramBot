using Microsoft.AspNetCore.Mvc.RazorPages;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Services;
using static System.Net.Mime.MediaTypeNames;

namespace FinanceTelegramBot.Controllers;

[TelegramRoute("/[controller]/[action]")]
public class CategoryController(
    CategoryTelegramService categoryTelegramService
    ) : ITelegramController
{
    [TelegramRoute("{type}/{page:null}")]
    public async Task GetAll(TransactionType type, int? page = 1)
    {
        await categoryTelegramService.SendCategoryList(type, page.Value);
    }

    public async Task GetAll()
    {
        await categoryTelegramService.ChooseTransactionType();
    }

    [TelegramRoute("{type}")]
    public async Task<bool> InitCreate(TransactionType type)
    {
        await categoryTelegramService.InitCreate(type);
        return false;
    }

    [TelegramRoute("{categoryId}")]
    public async Task Settings(long categoryId)
    {
        await categoryTelegramService.SendSettings(categoryId);
    }

    [TelegramRoute("{categoryId}")]
    public async Task<bool> Delete(long categoryId)
    {
        await categoryTelegramService.Delete(categoryId);

        return false;
    }

    [TelegramRoute("{categoryId}")]
    public async Task<bool> InitRename(long categoryId)
    {
        await categoryTelegramService.InitRename(categoryId);

        return false;
    }
}
