using ClusterManager.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClusterManager.Controllers;

[Route("api/monitor")]
[ApiController]
public class MonitorController(INodeManager _manager) : ControllerBase
{
    [HttpGet("nodes")]
    public async Task<IActionResult> GetAllNodesWithData()
    {
        var result = await _manager.GetAllNodesWithDataAsync();

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
    [HttpGet("node/{containerName}")]
    public async Task<IActionResult> GetNodeWithData(string containerName)
    {
        var result = await _manager.GetNodeWithDataAsync(containerName);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpGet("nodes/status")]
    public async Task<IActionResult> GetAllNodesStatus()
    {
        var result = await _manager.GetAllNodeStatusesAsync();

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, result.Error);
        }

        return Ok(result);
    }
    
    [HttpGet("node/status/{containerName}")]
    public async Task<IActionResult> GetNodesStatus(string containerName)
    {
        var result = await _manager.GetNodeStatusAsync(containerName);

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, result.Error);
        }

        return Ok(result);
    }
}
