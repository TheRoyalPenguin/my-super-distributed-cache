using Microsoft.AspNetCore.Mvc;
using Node.DTO;
using Node.Interfaces;
using Node.Models;

namespace Node.Controllers;

[Route("api/cache")]
[ApiController]
public class CacheController(ICacheStorage _cacheStorage) : ControllerBase
{
    [HttpGet("{key}")]
    public IActionResult Get(string key)
    {
        if (_cacheStorage.Cache.TryGetValue(key, out var item))
        {
            if (item.IsExpired())
            {
                _cacheStorage.Cache.TryRemove(key, out var _);
                return StatusCode(410, "TTL has expired.");
            }
            item.UpdateAccessTime();
            return Ok(item.Value);
        }
        return NotFound();
    }

    [HttpPut("single")]
    public IActionResult Set([FromBody] CacheItemRequestDto item)
    {
        var cacheItem = new CacheItem(
            item.Key,
            item.Value,
            item.TTL.HasValue ? item.TTL : null
        );

        _cacheStorage.Cache.AddOrUpdate(cacheItem.Key, cacheItem, (k, old) => cacheItem);
        return Ok();
    }
    [HttpPut("multiple")]
    public IActionResult Set([FromBody] List<CacheItemRequestDto> items)
    {
        foreach (var item in items)
        {
            var cacheItem = new CacheItem(
                item.Key,
                item.Value,
                item.TTL.HasValue ? item.TTL : null
            );

            _cacheStorage.Cache.AddOrUpdate(cacheItem.Key, cacheItem, (k, old) => cacheItem);
        }
        return Ok();
    }
    [HttpPost("delete/multiple")]
    public IActionResult Delete([FromBody] List<CacheItemRequestDto> items)
    {
        List<CacheItemRequestDto> deletedItems = new();
        List<string> notDeleted = new();

        foreach (var item in items)
        {
            var itemKey = item.Key;

            var res = _cacheStorage.Cache.TryRemove(itemKey, out var _);
            if (res)
                deletedItems.Add(item);
            else
            {
                notDeleted.Add(itemKey);
            }
        }
        if (notDeleted.Count > 0)
            return BadRequest("Ошибка удаления элементов с ключами: " + string.Join(", ", notDeleted));
        return Ok(deletedItems);
    }
    [HttpGet("all")]
    public IActionResult GetAll()
    {
        if (_cacheStorage.Cache != null)
        {
            var snapshot = _cacheStorage.Cache.ToArray();
            var result = snapshot.Select(kv => new CacheItemResponseDto
            {
                Key = kv.Key,
                Value = kv.Value.Value,
                CreatedAt = kv.Value.CreatedAt,
                LastAccessed = kv.Value.LastAccessed,
                TTL = kv.Value.TTL
            }).ToList();
            return Ok(result);
        }
        return NotFound();
    }
}
