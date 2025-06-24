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

        List<TelegramCommandAttribute> attributes = new();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    var findedAttributes = method.GetCustomAttributes<TelegramCommandAttribute>();
                    if (findedAttributes.Count() != 0)
                    {
                        attributes.AddRange(findedAttributes);
                    }
                }
            }
        }

        foreach (var attribute in attributes.OrderBy(c => c.Order))
            result.Add(new(attribute.Command.TrimStart('/'), attribute.Description));

        _commands = result;

        return result.DistinctBy(c => c.Command).ToList();
    }
    public List<BotCommand> GetTelegramCommands() => _commands ?? LoadTelegramCommands();
}
