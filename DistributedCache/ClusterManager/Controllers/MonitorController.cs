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
}
