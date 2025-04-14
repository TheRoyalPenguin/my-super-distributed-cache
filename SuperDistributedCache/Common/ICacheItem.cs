namespace Common;

public interface ICacheItem
{
    string Key { get; }
    object Value { get; }
    DateTime CreatedAt { get; }
    DateTime LastAccessed { get; }
    TimeSpan? TTL { get; }

    bool IsExpired();
}
