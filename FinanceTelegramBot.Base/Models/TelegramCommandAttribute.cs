namespace FinanceTelegramBot.Base.Models;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class TelegramCommandAttribute : Attribute
{
    public string Command { get; }
    public string Description { get; }
    public TelegramCommandAttribute(string command, string description)
    {
        Command = command;
        Description = description;
    }
}
