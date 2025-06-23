using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinanceTelegramBot.Base.Models;
public interface ICallbackDataStore
{
    Task<string> StoreDataAsync(object data);
    Task<string?> RetrieveDataAsync(string guid);
}
