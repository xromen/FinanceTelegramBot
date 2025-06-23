using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Base.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramControllers(this IServiceCollection services, TelegramControllersOptions options)
    {
        services.AddMemoryCache();

        services.AddScoped<StateService>();

        services.AddScoped<NavigationService>();

        services.AddScoped<RouteEnvironment>();

        services.TryAddSingleton<ICallbackDataStore, InMemoryCallbackDataStore>();

        services.AddSingleton(typeof(TelegramRouter), (serviceProvider) =>
        {
            var store = serviceProvider.GetRequiredService<ICallbackDataStore>();

            return new TelegramRouter(serviceProvider, store, options.DefaultController, options.DefaultMethodName, options.BackRoute);
        });

        services.AddSingleton<TelegramCommandRegistry>();

        services.AddTransient(c => new InlineKeyboardBuilder(options.BackRoute, options.MainMenuRoute));

        return services;
    }
}
