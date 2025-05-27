using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClusterManager.Controllers;

[Route("api/cluster")]
[ApiController]
public class ClusterController(INodeManager _manager, INodeRegistry _nodeRegistry) : ControllerBase
{
    [HttpGet("cache/{key}")]
    public async Task<IActionResult> GetCacheItem(string key)
    {
        var result = await _manager.GetCacheItemAsync(key);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpPut("cache")]
    public async Task<IActionResult> SetCacheItem([FromBody] CacheItemRequestDto item)
    {
        var result = await _manager.SetCacheItemAsync(item);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
    [HttpDelete("cache/delete/{itemKey}")]
    public async Task<IActionResult> DeleteCacheItem(string itemKey)
    {
        var result = await _manager.DeleteCacheItemAsync(itemKey);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
    [HttpPost("nodes/create/{containerName}/{copiesCount?}")]
    public async Task<IActionResult> CreateNode(string containerName, int copiesCount = 1)
    {
        if (copiesCount < 1 || copiesCount > 10)
            return BadRequest("Количество копий одного узла может быть от 1 до 10");
        var result = await _nodeRegistry.CreateNodeAsync(containerName, copiesCount);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpDelete("nodes/delete/{containerName}")]
    public async Task<IActionResult> DeleteNode([FromRoute] string containerName, [FromQuery] bool force)
    {
        var result = force == true ? await _nodeRegistry.ForceDeleteNodeByNameAsync(containerName) : await _nodeRegistry.DeleteNodeByNameAsync(containerName);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
}
