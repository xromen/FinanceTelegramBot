using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Diagnostics;

namespace FinanceTelegramBot.Base.Extensions;
public static class FinanceTelegramBotClientExtensions
{
    private static Dictionary<long, Message> currKeyboardMessage = new Dictionary<long, Message>();
    public static async Task<Message> SendMessageWithKeyboard(
        this ITelegramBotClient client,
        long userId,
        string text,
        InlineKeyboardMarkup? keyboard,
        ParseMode parseMode = ParseMode.MarkdownV2)
    {
        try
        {
            if (currKeyboardMessage.ContainsKey(userId))
            {
                var oldMessage = currKeyboardMessage[userId];
                await client.DeleteMessage(userId, oldMessage.Id);
            }
        }
        catch
        {

        }

        var currMessage = await client.SendMessage(userId, text, parseMode, replyMarkup: keyboard);

        if(keyboard != null)
        {
            currKeyboardMessage[userId] = currMessage;
        }

        return currMessage;
    }

    public static async Task<Message> TryEditMessage(this ITelegramBotClient bot, long userId, Message message, string? text, InlineKeyboardMarkup? keyboard, ParseMode parseMode = ParseMode.None)
    {
        try
        {
            return await bot.EditMessageText(userId, message.Id, text, replyMarkup: keyboard, parseMode: parseMode);
        }
        catch (Exception e) 
        {
            return await bot.SendMessageWithKeyboard(userId, text, keyboard, parseMode: parseMode);
        }
    }
}
