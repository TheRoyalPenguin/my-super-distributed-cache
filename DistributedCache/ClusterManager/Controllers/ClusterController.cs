using ClusterManager.DTO;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;
using Node.DTO;

namespace ClusterManager.Controllers;

[Route("api/cluster")]
[ApiController]
public class ClusterController : ControllerBase
{
    private readonly List<Uri> _nodes = new();
    private readonly object _lock = new();
    private readonly HttpClient _httpClient;

    public ClusterController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpPost("register")]
    public IActionResult RegisterNode([FromBody] NodeRegistrationRequest request)
    {
        lock (_lock)
        {
            var nodeUri = new Uri(request.Url);
            if (!_nodes.Contains(nodeUri))
            {
                _nodes.Add(nodeUri);
            }
        }

        return Ok();
    }

    [HttpGet("cache/{key}")]
    public async Task<IActionResult> GetCacheItem(string key)
    {
        try
        {
            var nodeUrl = GetNodeForItemKey(key);
            string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
            var requestUri = new Uri(baseUrl + "api/cache/" + Uri.EscapeDataString(key));

            var response = await _httpClient.GetAsync(requestUri);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Ok(content);
            }
            else
            {
                return StatusCode((int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("cache/all-nodes")]
    public async Task<IActionResult> GetFromAllNodes(string key)
    {
        try
        {
            var tasks = _nodes.Select<Uri, Task<object>>(async nodeUri =>
            {
                try
                {
                    var requestUri = new Uri(nodeUri, $"api/cache/{Uri.EscapeDataString(key)}");

                    var response = await _httpClient.GetAsync(requestUri);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return new
                        {
                            Node = nodeUri.ToString(),
                            Value = content,
                            Status = "Success",
                            StatusCode = (int)response.StatusCode
                        };
                    }
                    else
                    {
                        return new
                        {
                            Node = nodeUri.ToString(),
                            Value = (string)null,
                            Status = "Error",
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new
                    {
                        Node = nodeUri.ToString(),
                        Value = (string)null,
                        Status = "Exception",
                        Error = ex.Message,
                        StatusCode = 500
                    };
                }
            }).ToList();
            
            var results = await Task.WhenAll(tasks);

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Critical error: {ex.Message}");
        }
    }

    [HttpPut("cache/{key}")]
    public async Task<IActionResult> SetCacheItem(string key, [FromBody] CacheItemDto item)
    {
        try
        {
            var nodeUrl = GetNodeForItemKey(key);
            string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
            var requestUri = new Uri(baseUrl + "api/cache/" + Uri.EscapeDataString(key));

            var response = await _httpClient.PutAsJsonAsync(requestUri, item);
            if (response.IsSuccessStatusCode)
            {
                return Ok();
            }
            else
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    private ActionResult<Uri> GetNodeForItemKey(string key)
    {
        if (_nodes.Count == 0)
            return NotFound();

        int hash = key.GetHashCode();
        int index = Math.Abs(hash) % _nodes.Count;
        return _nodes[index];
    }
}