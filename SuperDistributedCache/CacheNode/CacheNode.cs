using Common;
using System.Collections.Concurrent;

namespace Node;

public class CacheNode : ICacheNode
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();

    public T Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var item) && !item.IsExpired())
        {
            return (T)item.Value;
        }

        return default!;
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        var cacheItem = new CacheItem(key, value, ttl);
        _cache.AddOrUpdate(cacheItem.Key, cacheItem, (key, existingItem) => cacheItem);
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }
}
