using Node.Models;
using System.Collections.Concurrent;

namespace Node.Interfaces;

public interface ICacheStorage
{
    ConcurrentDictionary<string, CacheItem> Cache { get; }
}
