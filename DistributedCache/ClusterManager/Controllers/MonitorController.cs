using ClusterManager.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClusterManager.Controllers;

[Route("api/monitor")]
[ApiController]
public class MonitorController(INodesService _nodesService) : ControllerBase
{
    [HttpGet("nodes")]
    public async Task<IActionResult> GetAllNodesWithData()
    {
        var result = await _nodesService.GetAllNodesWithDataAsync();

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
    
    [HttpGet("node/{key}")]
    public async Task<IActionResult> GetNodeWithData(string key)
    {
        var result = await _nodesService.GetNodeWithDataAsync(key);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpGet("nodes/status")]
    public async Task<IActionResult> GetAllNodesStatus()
    {
        var result = await _nodesService.GetAllNodeStatusesAsync();

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, result.Error);
        }

        return Ok();
    }
    
    [HttpGet("node/status/{key}")]
    public async Task<IActionResult> GetNodesStatus(string key)
    {
        var result = await _nodesService.GetNodeStatusAsync(key);

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, result.Error);
        }

        return Ok();
    }
}
