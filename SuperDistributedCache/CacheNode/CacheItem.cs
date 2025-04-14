using Common;

namespace Node;

public class CacheItem : ICacheItem
{
    public string Key { get; private set; }
    public object Value { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastAccessed { get; private set; }
    public TimeSpan? TTL { get; private set; } 

    public bool IsExpired()
    {
        return TTL.HasValue && DateTime.UtcNow - CreatedAt > TTL.Value;  
    }

    public CacheItem(string key, object value, TimeSpan? ttl = null)
    {
        Key = key;
        Value = value;
        CreatedAt = DateTime.UtcNow;
        LastAccessed = CreatedAt;
        if (ttl != null)
        {
            TTL = ttl.Value;
        }
    }
}
