using ClusterManager.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Node.DTO;

namespace ClusterManager.Controllers;

[Route("api/cluster")]
[ApiController]
public class ClusterController(INodesService _nodesService) : ControllerBase
{
    [HttpGet("cache/{key}")]
    public async Task<IActionResult> GetCacheItem(string key)
    {
        var result = await _nodesService.GetCacheItemAsync(key);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpPut("cache")]
    public async Task<IActionResult> SetCacheItem([FromBody] CacheItemDto item)
    {
        var result = await _nodesService.SetCacheItemAsync(item);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpPost("nodes/create/{containerName}")]
    public async Task<IActionResult> CreateNode(string containerName)
    {
        var result = await _nodesService.CreateNodeAsync(containerName);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
}
