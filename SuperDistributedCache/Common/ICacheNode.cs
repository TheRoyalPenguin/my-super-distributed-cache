namespace Common;

public interface ICacheNode
{
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? ttl);
    void Remove(string key);
}
