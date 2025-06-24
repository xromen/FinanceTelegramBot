using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinanceTelegramBot.Base.Models;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class TelegramRouteAttribute : Attribute
{
    public string Template { get; }
    public TelegramRouteAttribute([StringSyntax("Route")] string template) => Template = template;
}
