using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using FinanceTelegramBot.Base.Models;

namespace FinanceTelegramBot.Base.Services;
public class InMemoryCallbackDataStore(IMemoryCache cache) : ICallbackDataStore
{
    //public Dictionary<string, string> _store = new();
    public Task<string?> RetrieveDataAsync(string guid)
    {
        if(!cache.TryGetValue<string>(guid, out var data))
            return Task.FromResult<string?>(null);

        //if (!_store.ContainsKey(guid))
        //    return Task.FromResult<string?>(null);

        return Task.FromResult(data);
    }

    public Task<string> StoreDataAsync(object data)
    {
        var id = Guid.NewGuid().ToString();
        var json = JsonSerializer.Serialize(data);
        cache.Set(id, json, TimeSpan.FromMinutes(30));
        //_store.Add(id, json);
        return Task.FromResult(id);
    }
}
