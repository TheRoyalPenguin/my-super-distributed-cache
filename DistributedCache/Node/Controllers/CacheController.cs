using Microsoft.AspNetCore.Mvc;
using Node.DTO;
using Node.Models;
using System.Collections.Concurrent;


namespace Node.Controllers;

[Route("api/cache")]
[ApiController]
public class CacheController : ControllerBase
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();

    [HttpGet("{key}")]
    public ActionResult<object> Get(string key)
    {
        if (_cache.TryGetValue(key, out var item) && !item.IsExpired())
        {
            return Ok(item.Value);
        }
        return NotFound();
    }

    [HttpPut("{key}")]
    public IActionResult Set(string key, [FromBody] CacheItemDto item)
    {
        var cacheItem = new CacheItem(
            key,
            item.Value,
            item.TTL.HasValue ? item.TTL : null
        );

        _cache.AddOrUpdate(key, cacheItem, (k, old) => cacheItem);
        return Ok();
    }
    
    [HttpGet("items")]
    public ActionResult<Dictionary<string, object>> GetAllItems()
    {
        var items = _cache
            .Where(kv => !kv.Value.IsExpired())
            .ToDictionary(kv => kv.Key, kv => kv.Value.Value);
    
        return Ok(items);
    }

    [HttpGet("status")]
    public ActionResult<Dictionary<string, object>> GetStatus()
    {
        var result = new Dictionary<string, object>();
        
        return Ok(result);
    }
    
    [HttpGet("health")]
    public ActionResult<HealthResponse> GetHealthStatus()
    {
        var activeItems = _cache.Count(kv => !kv.Value.IsExpired());
    
        return new HealthResponse
        {
            ItemsCount = activeItems,
            Status = "Healthy"
        };
    }
    [HttpGet("item/{key}")]
    public IActionResult GetCacheItemDetails(string key)
    {
        if (_cache.TryGetValue(key, out var item) && !item.IsExpired())
        {
            item.LastAccessed = DateTime.UtcNow;
        
            return Ok(new
            {
                Item = new
                {
                    Key = item.Key,
                    Value = item.Value,
                    CreatedAt = item.CreatedAt,
                    LastAccessed = item.LastAccessed,
                    TTL = item.TTL,
                    ExpiresAt = item.TTL.HasValue ? item.CreatedAt.Add(item.TTL.Value) : (DateTime?)null,
                    IsExpired = item.IsExpired()
                },
                CacheStats = new
                {
                    TotalItems = _cache.Count,
                    ExpiredItems = _cache.Count(kv => kv.Value.IsExpired()),
                    MemoryUsage = $"{GC.GetTotalMemory(false) / 1024 / 1024} MB"
                }
            });
        }
    
        return NotFound(new { Key = key, Status = "Not found or expired" });
    }
}
