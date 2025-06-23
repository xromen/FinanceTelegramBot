using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Services;

namespace FinanceTelegramBot.Controllers;

[TelegramRoute("/[controller]/[action]")]
public class FamilyController(FamilyTelegramService familyTelegramService) : ITelegramController
{
    [TelegramRoute("{ownerId}")]
    public async Task<bool> Create(long ownerId)
    {
        await familyTelegramService.Create(ownerId);

        return false;
    }

    public async Task Settings()
    {
        await familyTelegramService.SendSettings();
    }

    [TelegramRoute("{familyId}")]
    public async Task<bool> Delete(long familyId)
    {
        await familyTelegramService.Delete(familyId);

        return false;
    }

    [TelegramRoute("{memberId}")]
    public async Task<bool> Banish(long memberId)
    {
        await familyTelegramService.BanishMember(memberId);

        return false;
    }

    public async Task<bool> InitAddMember()
    {
        await familyTelegramService.InitAddMember();

        return false;
    }
}
