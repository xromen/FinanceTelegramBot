using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FinanceTelegramBot.Base.Models;
public class RouteEntry
{
    public Regex Pattern { get; set; } = null!;
    public Type Controller { get; set; } = null!;
    public MethodInfo Method { get; set; } = null!;
}
