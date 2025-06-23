using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Extensions.Caching.Memory;
using FinanceTelegramBot.Base.Models;

namespace FinanceTelegramBot.Base.Services;
public class StateService(IMemoryCache cache)
{
    //public void OnUserState(ITelegramBotClient botClient, Message message, UserState state)
    //{
    //    if (string.IsNullOrEmpty(message.Text))
    //    {
    //        botClient.SendMessage(message.Chat.Id, "Неизвестное состояние");
    //    }
    //    else
    //    {
    //        botClient.SendMessage(message.Chat.Id, "Результат");
    //        this.RemoveState(state);
    //    }
    //}

    public UserState? GetCurrentState(long userId) =>
        cache.Get<UserState>($"UserState:{userId}");

    public void CreateOrUpdateState(UserState state)
    {
        var key = $"UserState:{state.UserId}";
        cache.Set(key, state, TimeSpan.FromMinutes(30));
    }

    public void RemoveState(UserState state)
    {
        var key = $"UserState:{state.UserId}";
        cache.Remove(key);
    }
}
