using Node.Models;
using System.Collections.Concurrent;

namespace Node.Services;

public interface ICacheStorage
{
    ConcurrentDictionary<string, CacheItem> Cache { get; }
}
