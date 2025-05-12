using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> SetCacheItem([FromBody] CacheItemRequestDto item)
    {
        var result = await _nodesService.SetCacheItemAsync(item);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpPost("nodes/create/{containerName}/{copiesCount?}")]
    public async Task<IActionResult> CreateNode(string containerName, int copiesCount = 1)
    {
        if (copiesCount < 1 || copiesCount > 10)
            return BadRequest("Количество копий одного узла может быть от 1 до 10");
        var result = await _nodesService.CreateNodeAsync(containerName, copiesCount);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
}
