namespace FinanceTelegramBot.Base.Models;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class TelegramCommandAttribute : Attribute
{
    public string Command { get; }
    public string Description { get; }
    public int Order { get; }
    public TelegramCommandAttribute(string command, string description, int order = 10)
    {
        Command = command;
        Description = description;
        Order = order;
    }
}
