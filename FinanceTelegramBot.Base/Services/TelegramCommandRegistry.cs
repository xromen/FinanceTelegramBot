using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Bot.Types;
using FinanceTelegramBot.Base.Models;

namespace FinanceTelegramBot.Base.Services;
public class TelegramCommandRegistry
{
    private List<BotCommand>? _commands;
    private List<BotCommand> LoadTelegramCommands()
    {
        var result = new List<BotCommand>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var attribute = method.GetCustomAttribute<TelegramCommandAttribute>();
                    if (attribute != null)
                    {
                        result.Add(new(attribute.Command.TrimStart('/'), attribute.Description));
                    }
                }
            }
        }

        _commands = result;

        return result.DistinctBy(c => c.Command).ToList();
    }
    public List<BotCommand> GetTelegramCommands() => _commands ?? LoadTelegramCommands();
}
