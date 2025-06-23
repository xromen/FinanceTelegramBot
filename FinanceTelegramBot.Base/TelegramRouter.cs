using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FinanceTelegramBot.Base.Models;
using FinanceTelegramBot.Base.Services;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Base;
//Singletone
public class TelegramRouter
{
    private readonly List<RouteEntry> _routes = new();
    private readonly IServiceProvider _provider;
    private readonly ICallbackDataStore _store;
    private readonly Type _defaultController;
    private readonly string _defaultMethodName;
    private readonly string _backRoute;

    public TelegramRouter(IServiceProvider provider, ICallbackDataStore store, Type defaultController, string defaultMethodName, string backRoute)
    {
        _provider = provider;
        _store = store;
        _defaultController = defaultController;
        _defaultMethodName = defaultMethodName;
        _backRoute = backRoute;

        if (!_defaultController.IsAssignableTo(typeof(ITelegramController)))
        {
            throw new ArgumentException($"Стандартный контроллер должен наследовать {nameof(ITelegramController)}");
        }

        var controllers = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITelegramController).IsAssignableFrom(t));

        foreach (var controllerType in controllers)
        {
            var controllerRouteAttribute = controllerType.GetCustomAttribute<TelegramRouteAttribute>();
            string controllerTemplate = string.Empty;

            if(controllerRouteAttribute != null)
            {
                controllerTemplate = controllerRouteAttribute.Template;
            }

            foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var actionAttribute = method.GetCustomAttribute<TelegramRouteAttribute>();
                string actionTemplate = string.Empty;
                
                if(actionAttribute != null)
                {
                    actionTemplate = actionAttribute.Template;
                }
                else if(!controllerTemplate.Contains("[action]", StringComparison.OrdinalIgnoreCase))
                {
                    actionTemplate = method.Name;
                }

                _routes.Add(ParseRoute(GetTemplate(controllerTemplate, actionTemplate), controllerType, method));
            }
        }
    }

    public async Task<bool> TryHandle(Update update)
    {
        var scope = _provider.CreateAsyncScope();
        var env = scope.ServiceProvider.GetRequiredService<RouteEnvironment>();
        var stateService = scope.ServiceProvider.GetRequiredService<StateService>();
        var navigationService = scope.ServiceProvider.GetRequiredService<NavigationService>();

        env.Update = update;

        string data = string.Empty;

        switch (update.Type)
        {
            case UpdateType.Message:
                if (string.IsNullOrEmpty(update.Message!.Text) && update.Message!.Photo == null)
                    return false;

                env.UserId = update.Message.From!.Id;
                var state = stateService.GetCurrentState(env.UserId);

                data = update.Message!.Text;

                if (state != null)
                {
                    await state.Action.Invoke(update, state, scope);
                    return false;
                }

                break;
            case UpdateType.CallbackQuery:
                if (string.IsNullOrEmpty(update.CallbackQuery!.Data))
                    return false;

                env.UserId = update.CallbackQuery.From!.Id;
                data = update.CallbackQuery.Data;
                break;
        }

        foreach (var route in _routes)
        {
            if(!string.IsNullOrEmpty(data) && TryCreateArgs(data, route, out var args))
            { 
                var controllerInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, route.Controller);

                var result = route.Method.Invoke(controllerInstance, args);
                if (result is Task task)
                    await task;

                bool success = true;
                if(result is Task<bool> boolTask)
                {
                    success = await boolTask;
                }

                if(!data.Equals(_backRoute, StringComparison.OrdinalIgnoreCase) && success)
                    navigationService.Push(update, env.UserId);

                return true;
            }
        }

        var defaultControllerInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, _defaultController);
        var defaultControllerMethod = _defaultController.GetMethod(_defaultMethodName)
            ?? throw new Exception($"Метод {_defaultMethodName} не определен в контроллере {_defaultController.Name}");

        var defaultResult = defaultControllerMethod.Invoke(defaultControllerInstance, []);
        if (defaultResult is Task defaultTask)
            await defaultTask;

        return false;
    }

    private RouteEntry ParseRoute(string template, Type controller, MethodInfo method)
    {
        var controllerName = controller.Name;

        if (controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            controllerName = controllerName[..^"Controller".Length];

        var actionName = method.Name;

        // Подстановка [controller] и [action]
        template = template
            .Replace("[controller]", controllerName.ToLowerInvariant())
            .Replace("[action]", actionName.ToLowerInvariant());


        // Преобразуем {param:type} в regex-группы
        //var pattern = Regex.Replace(template, @"\{(\w+)(?::(null))?\}", m => 
        //{
        //    var name = m.Groups[1].Value;
        //    var typeRaw = m.Groups[2].Value;

        //    var optional = typeRaw.Equals("null", StringComparison.OrdinalIgnoreCase);

        //    var regexBody = @"(?<" + m.Groups[1].Value + @">[^ ]+)";

        //    if (optional || string.IsNullOrEmpty(typeRaw))
        //    {
        //        return $@"(?:\/+{regexBody})?";
        //    }

        //    return $@"\s+{regexBody}";
        //});
        var pattern = Regex.Replace(template, @"/\{(\w+):null\}", @"(?:/(?<$1>[^/]+))?");
        pattern = Regex.Replace(pattern, @"\{(\w+)\}", @"(?<$1>[^/]+)");

        pattern = "^" + pattern;

        return new RouteEntry
        {
            Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase),
            Method = method,
            Controller = controller,
        };
    }

    private bool TryCreateArgs(string data, RouteEntry route, out object?[] args)
    {
        var parameters = route.Method.GetParameters();
        args = new object?[parameters.Length];

        var match = route.Pattern.Match(data);
        if (!match.Success)
        {
            return false;
        }

        var splitted = data.Split(' ');
        //string? storedArgsJson = default;
        //JsonDocument? storedArgsDocument = default;

        //if (splitted.Length == 2 && Guid.TryParse(splitted[1], out var guid))
        //{
        //    storedArgsJson = _store.RetrieveDataAsync(guid.ToString()).GetAwaiter().GetResult();
        //    JsonSerializer.SerializeToDocument(storedArgsJson);
        //}

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            var group = match.Groups[parameter.Name!.ToLowerInvariant()];

            var isNullable = Nullable.GetUnderlyingType(parameter.ParameterType) != null;
            var actualType = isNullable ? Nullable.GetUnderlyingType(parameter.ParameterType)! : parameter.ParameterType;

            var fromDataStore = parameter.GetCustomAttribute<FromDataStoreAttribute>();

            if (group.Success && fromDataStore != null && Guid.TryParse(group.Value, out var guid))
            {
                var storedData = _store.RetrieveDataAsync(guid.ToString()).GetAwaiter().GetResult();
                var obj = JsonSerializer.Deserialize(storedData!, parameter.ParameterType);
                args[i] = obj;
                continue;
            }

            if (group.Success)
            {
                var value = group.Value;
                args[i] = ConvertToParameterType(value, actualType);
                continue;
            }

            if (isNullable)
            {
                if(parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    args[i] = GetDefault(parameter.ParameterType);
                }

                continue;
            }

            //if(storedArgsJson == null)
            //{
            //    return false;
            //}

            //var obj = JsonSerializer.Deserialize(storedArgsJson, parameter.ParameterType);

            //if(obj != null)
            //{
            //    args[i] = obj;
            //    continue;
            //}

            //if(storedArgsDocument!.RootElement.TryGetProperty(parameter.Name!, out var json))
            //{
            //    var value = json.Deserialize(parameter.ParameterType);
            //    args[i] = value;
            //}
            //else
            //{
            //    var value = GetDefault(parameter.ParameterType);
            //    args[i] = value;
            //}
        }

        return true;
    }

    private static object? ConvertToParameterType(string? input, Type targetType)
    {
        if (input == null)
        {
            if (IsNullableType(targetType)) return null;
            throw new InvalidOperationException("Value is required but was not provided.");
        }

        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (actualType.IsEnum)
        {
            if (Enum.TryParse(actualType, input, ignoreCase: true, out var enumValue))
                return enumValue;

            throw new InvalidCastException($"Cannot convert '{input}' to enum {actualType.Name}.");
        }

        return Convert.ChangeType(input, actualType);
    }

    private string GetTemplate(string controllerTemplate, string actionTemplate)
    {
        string result = String.Empty;

        if (!string.IsNullOrEmpty(controllerTemplate))
            result += controllerTemplate.ToLowerInvariant();

        if (!string.IsNullOrEmpty(actionTemplate))
        {
            if ((!string.IsNullOrEmpty(result) && result[..^1] != "/") && actionTemplate[0] != '/')
            {
                result += "/";
            }
            result += actionTemplate.ToLowerInvariant();
        }

        return result;
    }

    private object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
    private static bool IsNullableType(Type type) =>
        Nullable.GetUnderlyingType(type) != null || !type.IsValueType;

}
