using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Services;

namespace FinanceTelegramBot.Controllers;

[TelegramRoute("/[controller]/[action]")]
public class KeywordController(KeywordTelegramService keywordTelegramService) : ITelegramController
{
    [TelegramRoute("{categoryId}/{page:null}")]
    public async Task GetAll(long categoryId, int? page = 1)
    {
        await keywordTelegramService.SendKeywordsList(categoryId, page.Value);
    }

    [TelegramRoute("{categoryId}")]
    public async Task<bool> InitCreate(long categoryId)
    {
        await keywordTelegramService.InitCreate(categoryId);
        return false;
    }

    [TelegramRoute("{keywordId}")]
    public async Task Settings(long keywordId)
    {
        await keywordTelegramService.SendSettings(keywordId);
    }

    [TelegramRoute("{keywordId}")]
    public async Task<bool> Delete(long keywordId)
    {
        await keywordTelegramService.Delete(keywordId);

        return false;
    }

    [TelegramRoute("{categoryId}")]
    public async Task<bool> InitRename(long categoryId)
    {
        await keywordTelegramService.InitRename(categoryId);
        return false;
    }
}
