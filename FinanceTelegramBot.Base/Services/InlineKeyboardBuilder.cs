using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace FinanceTelegramBot.Base.Services;

public class InlineKeyboardBuilder
{
    private List<InlineKeyboardButton[]> _buttons = new();
    private List<InlineKeyboardButton> _lineButtons = new();
    private string _backRoute;
    private string _mainMenuRoute;

    public InlineKeyboardBuilder(string backRoute, string mainMenuRoute)
    {
        _backRoute = backRoute;
        _mainMenuRoute = mainMenuRoute;
    }

    public InlineKeyboardBuilder(IEnumerable<IEnumerable<InlineKeyboardButton>>? keyboard = null)
    {
        if (keyboard != null)
        {
            _buttons = keyboard.Select(c => c.ToArray()).ToList();
        }
    }
    public InlineKeyboardBuilder AppendCallbackData(string text, string callbackData)
    {
        if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(callbackData))
        {
            _lineButtons.Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        }
        return this;
    }

    public InlineKeyboardBuilder AppendBackButton()
    {
        if (string.IsNullOrWhiteSpace(_backRoute))
        {
            throw new Exception("_backRoute Не определен");
        }
        return AppendCallbackData("🔙 Назад", _backRoute);
    }

    public InlineKeyboardBuilder AppendToMainMenuButton()
    {
        if (string.IsNullOrWhiteSpace(_mainMenuRoute))
        {
            throw new Exception("_mainMenuRoute Не определен");
        }
        return AppendCallbackData("≡ Меню", _mainMenuRoute);
    }

    public InlineKeyboardBuilder AppendLine()
    {
        if (_lineButtons.Count > 0)
        {
            _buttons.Add(_lineButtons.ToArray());
            _lineButtons.Clear();
        }
        return this;
    }

    public InlineKeyboardBuilder AppendPagination(int currentPage, int totalPages, Func<int, string> createCallbackData)
    {
        if (totalPages <= 1) return this;

        var prevCallback = currentPage - 1 < 1 ? "EmptyRoute.Prefix" : createCallbackData(currentPage - 1);// string.Format(callbackDataFormat, currentPage - 1);
        AppendCallbackData("<", prevCallback);

        AppendCallbackData($"{currentPage}/{totalPages}", "EmptyRoute.Prefix");

        var nextCallback = currentPage + 1 > totalPages ? "EmptyRoute.Prefix" : createCallbackData(currentPage + 1);// string.Format(callbackDataFormat, currentPage + 1);
        AppendCallbackData(">", nextCallback);

        AppendLine();
        return this;
    }

    public InlineKeyboardMarkup Build()
    {
        AppendLine();
        return new(_buttons);
    }
}
