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
}
