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
}
