using System;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using FinanceTelegramBot.Base;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Controllers;

public class UserController(
    ITelegramBotClient bot, 
    RouteEnvironment env,
    ICallbackDataStore store) : ITelegramController
{
    

    [TelegramRoute("/[controller]/[action]")]
    public async void Get(UserGetDto dto)
    {
        var button = InlineKeyboardButton.WithCallbackData("В меню", "/start");

        await bot.EditMessageText(env.UserId, env.Update.CallbackQuery.Message.Id, dto.Id + " " + dto.UserName, replyMarkup: new InlineKeyboardMarkup(button));
    }
}

public class UserGetDto
{
    public long Id { get; set; }
    public string UserName { get; set; }
}
