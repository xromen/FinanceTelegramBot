
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using User = FinanceTelegramBot.Data.Entities.User;

namespace FinanceTelegramBot.Services;

public class FamilyTelegramService(
    FamilyService familyService,
    RouteEnvironment env,
    InlineKeyboardBuilder keyboardBuilder,
    StateService stateService,
    ITelegramBotClient bot
    )
{
    public async Task Create(long ownerId)
    {
        var family = new Family() { OwnerId = ownerId };

        try
        {
            await familyService.CreateAsync(family);
        }
        catch(BusinessException e) 
        {
            await bot.SendMessageWithKeyboard(env.UserId, e.Message, null);
        }

        await bot.DeleteMessage(env.UserId, env.Update.CallbackQuery.Message.Id);

        await bot.SendMessage(env.UserId, "✅ Семья успешно создана");

        await SendSettings();
    }

    public async Task SendSettings()
    {
        StringBuilder responseTextBuilder = new StringBuilder("👨‍👩‍👦 Управление семьей\n");

        var family = await familyService.GetFamilyByMemberId(env.UserId);

        if (family == null)
        {
            responseTextBuilder.AppendLine("У вас нет семьи :(\n\n _Вы можете создать новую или попросите чтобы вас добавили в существующую_");

            keyboardBuilder.AppendCallbackData("➕ Создать новую семью", $"/Family/Create/{env.UserId}").AppendLine();
        }
        else if (family.OwnerId == env.UserId)
        {
            responseTextBuilder.AppendLine($"Глава семьи: {family.Owner.FirstName} (Вы)");

            responseTextBuilder.AppendLine("\n🗑 Нажмите на участника для исключения");

            foreach (var member in family.Members.Where(c => c.Id != env.UserId))
            {
                keyboardBuilder.AppendCallbackData(member.Username + " " + member.FirstName + " " + member.LastName, $"/Family/Banish/{member.Id}");
                keyboardBuilder.AppendLine();
            }

            keyboardBuilder.AppendCallbackData("➕ Добавить члена семьи", "/Family/InitAddMember").AppendLine();

            keyboardBuilder.AppendCallbackData("❌ Удалить семью", $"/Family/Delete/{family.Id}").AppendLine();
        }
        else
        {
            responseTextBuilder.AppendLine($"Глава семьи: {family.Owner.FirstName}");
            responseTextBuilder.AppendLine("Члены семьи:");

            foreach (var person in family.Members)
            {
                responseTextBuilder.AppendLine($"    {person.FirstName}");
            }

            keyboardBuilder.AppendCallbackData("❌ Покинуть семью", $"/Family/Banish/{env.UserId}").AppendLine();
        }

        keyboardBuilder.AppendBackButton().AppendToMainMenuButton();

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery!.Message!, responseTextBuilder.ToString(), keyboardBuilder.Build(), ParseMode.Markdown);
    }

    public async Task Delete(long familyId)
    {
        var family = await familyService.GetFamilyById(familyId);

        if(family == null)
        {
            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "У вас нет семьи", null);
            return;
        }

        if (family.OwnerId != env.UserId)
        {
            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "Вы не глава семьи", null);
            return;
        }

        await familyService.DeleteAsync(familyId);

        await SendSettings();
    }

    public async Task BanishMember(long memberId)
    {
        try
        {
            var family = await familyService.GetFamilyByMemberId(memberId);

            if(family == null)
            {
                await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "Пользователь не состоит в семье", null);
                return;
            }

            if(family.OwnerId != env.UserId)
            {
                await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "Вы не глава семьи", null);
                return;
            }

            var user = family.Members.First(c => c.Id == memberId);

            family.Members.Remove(user);

            await familyService.UpdateAsync(family);

            keyboardBuilder.AppendToMainMenuButton();

            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, $"Пользователь *{user.FirstName}* исключен из вашей семьи", keyboardBuilder.Build(), ParseMode.Markdown);
        }
        catch(BusinessException e)
        {
            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, e.Message, null);
            return;
        }
    }

    public async Task InitAddMember()
    {
        var family = await familyService.GetFamilyByOwnerId(env.UserId);

        if(family == null)
        {
            await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message, "У вас нет семьи или вы не ее глава", null);
            return;
        }

        var text = $"Введи *имя пользователя* или его *Id* 👇🏼";
        keyboardBuilder.AppendCallbackData("⨉ Отмена", "/navigation/back");

        stateService.CreateOrUpdateState(new()
        {
            UserId = env.UserId,
            Action = AddMember
        });

        await bot.TryEditMessage(env.UserId, env.Update.CallbackQuery.Message!, text, keyboardBuilder.Build(), ParseMode.Markdown);
    }

    private async Task AddMember(Update update, UserState state, IServiceScope scope)
    {
        var memberIdOrUsername = update.Message?.Text;
        if (string.IsNullOrWhiteSpace(memberIdOrUsername))
        {
            await bot.SendMessageWithKeyboard(env.UserId, "Имя пользователя или его id не может быть пустым", null);
            return;
        }

        var scopedFamilyService = scope.ServiceProvider.GetRequiredService<FamilyService>();
        var scopedUserService = scope.ServiceProvider.GetRequiredService<UserService>();
        var scopedStateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var scopedKeyboardBuilder = scope.ServiceProvider.GetRequiredService<InlineKeyboardBuilder>();

        var family = await scopedFamilyService.GetFamilyByOwnerId(env.UserId);

        try
        {
            User? member = null;

            if(long.TryParse(memberIdOrUsername, out var memberId))
            {
                member = await scopedFamilyService.AddFamilyMemberByIdAsync(family!.Id, memberId);
            }
            else
            {
                member = await scopedFamilyService.AddFamilyMemberByUsernameAsync(family!.Id, memberIdOrUsername);
            }

            stateService.RemoveState(state);

            scopedKeyboardBuilder.AppendToMainMenuButton();

            await bot.SendMessageWithKeyboard(env.UserId, $"✅ Пользователь *{member.FirstName}* добавлен в вашу семью", scopedKeyboardBuilder.Build(), ParseMode.MarkdownV2);
        }
        catch (BusinessException ex)
        {
            await bot.SendMessage(env.UserId, ex.Message);
        }
    }
}
