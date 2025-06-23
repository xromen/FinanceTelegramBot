namespace FinanceTelegramBot.Base.Models;
public class TelegramControllersOptions
{
    public Type? DefaultController { get; set; }
    public string? DefaultMethodName { get; set; }
    public string BackRoute { get; set; } = "/Navigation/Back";
    public string MainMenuRoute { get; set; } = "/Navigation/MainMenu";
}
