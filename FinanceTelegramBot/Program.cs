using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using FinanceTelegramBot.Base.Extensions;
using FinanceTelegramBot.Controllers;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Models;
using FinanceTelegramBot.Services;
using System.Globalization;

namespace FinanceTelegramBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cultureInfo = new CultureInfo("ru-RU");
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            var apiKey = Environment.GetEnvironmentVariable("TG_KEY") ??
                         Environment.GetEnvironmentVariable("TG_KEY", EnvironmentVariableTarget.User) ??
                         throw new Exception("Не установлено значение ключа TG_KEY");

            var pgCs = Environment.GetEnvironmentVariable("PG_CS") ??
                       Environment.GetEnvironmentVariable("PG_CS", EnvironmentVariableTarget.User) ??
                       throw new Exception("Не установлено значение ключа PG_CS");

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(pgCs)
            .UseSnakeCaseNamingConvention());

            builder.Services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(apiKey));

            builder.Services.AddScoped<MoneyTransactionService>()
                .AddScoped<MoneyTransactionTelegramService>();

            builder.Services.AddScoped<UserService>();

            builder.Services.AddScoped<FamilyService>()
                .AddScoped<FamilyTelegramService>();

            builder.Services.AddScoped<KeywordService>()
                .AddScoped<KeywordTelegramService>();

            builder.Services.AddScoped<CategoryService>()
                .AddScoped<CategoryTelegramService>();

            builder.Services.AddScoped<CategoryAndKeywordNamesService>();

            builder.Services.AddScoped(provider => new Lazy<KeywordService>(() => provider.GetRequiredService<KeywordService>()));
            builder.Services.AddScoped(provider => new Lazy<CategoryService>(() => provider.GetRequiredService<CategoryService>()));

            builder.Services.AddScoped<ProverkaChekaApi>();

            builder.Services.AddScoped<DefaultCommandService>();

            builder.Services.AddTelegramControllers(new()
            {
                DefaultController = typeof(DefaultController), 
                DefaultMethodName = nameof(DefaultController.Index), 
                BackRoute = "/Navigation/Back",
                MainMenuRoute = "/Navigation/MainMenu"
            });
            builder.Services.AddScoped<RouteEnvironment>();

            builder.Services.AddHostedService<BotBackgroundService>();

            var app = builder.Build();

            app.MapGet("/", () => "Hello World!");

            //var scope = app.Services.CreateScope();

            //var router = scope.ServiceProvider.GetRequiredService<TelegramRouter>();
            //var store = scope.ServiceProvider.GetRequiredService<ICallbackDataStore>();

            app.Run();
        }
    }
}
