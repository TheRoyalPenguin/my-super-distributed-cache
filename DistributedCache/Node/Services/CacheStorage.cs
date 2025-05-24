using Node.Interfaces;
using Node.Models;
using System.Collections.Concurrent;

namespace Node.Services;

public class CacheStorage : ICacheStorage
{
    public ConcurrentDictionary<string, CacheItem> Cache { get; } = new();
}
