using ClusterManager.DTO;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Node.DTO;
using Node.Models;

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


    [HttpGet("nodes-status")]
    public async Task<IActionResult> GetNodeStatuses()
    {
        try
        {
            var tasks = _nodes.Select<Uri, Task<(bool IsActive, object NodeInfo)>>(async nodeUri =>
            {
                try
                {
                    string baseUrl = nodeUri.ToString().EndsWith("/") ? nodeUri.ToString() : nodeUri.ToString() + "/";
                    var requestUri = new Uri(baseUrl + "api/nodes-status");

                    var response = await _httpClient.GetAsync(requestUri);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var cacheItems = JsonConvert.DeserializeObject<List<CacheItem>>(content);

                        var nodeInfo = new
                        {
                            NodeUrl = baseUrl,
                            Items = cacheItems.Select(item => new
                            {
                                Key = item.Key,
                                Value = item.Value,
                                CreatedAt = item.CreatedAt,
                                LastAccessed = item.LastAccessed,
                                TTL = item.TTL,
                                IsExpired = item.IsExpired()
                            }),
                            Status = "Success"
                        };

                        return (true, (object)nodeInfo);
                    }

                    return (false, new
                    {
                        NodeUrl = baseUrl,
                        Items = new List<object>(),
                        Status = "Error",
                        Error = $"Status code: {(int)response.StatusCode}"
                    });
                }
                catch (Exception ex)
                {
                    return (false, new
                    {
                        NodeUrl = nodeUri.ToString(),
                        Items = new List<object>(),
                        Status = "Error",
                        Error = ex.Message
                    });
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            var activeNodes = results.Where(r => r.IsActive).Select(r => r.NodeInfo).ToList();
            var inactiveNodes = results.Where(r => !r.IsActive).Select(r => r.NodeInfo).ToList();

            var finalResult = new
            {
                ActiveNodes = activeNodes,
                InactiveNodes = inactiveNodes
            };

            return Ok(finalResult);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Status = "Critical Error",
                Error = ex.Message
            });
        }
    }


    [HttpGet("node-item/{key}")]
    public async Task<IActionResult> GetNode(string key)
    {
        try
        {
            var nodeUrl = GetNodeForItemKey(key);
            string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
            var requestUri = new Uri(baseUrl + "api/cache/full-item/" + Uri.EscapeDataString(key));

            var response = await _httpClient.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var cacheItem = JsonConvert.DeserializeObject<CacheItem>(content);

                return Ok(new
                {
                    Key = cacheItem.Key,
                    Value = cacheItem.Value,
                    CreatedAt = cacheItem.CreatedAt,
                    LastAccessed = cacheItem.LastAccessed,
                    TTL = cacheItem.TTL,
                    IsExpired = cacheItem.IsExpired(),
                    NodeUrl = baseUrl
                });
            }
            else
            {
                return StatusCode((int)response.StatusCode, new
                {
                    Key = key,
                    Error = "Item not found or error on node",
                    StatusCode = (int)response.StatusCode,
                    NodeUrl = baseUrl
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Key = key,
                Error = ex.Message,
                StatusCode = 500
            });
        }
    }

    [HttpGet("all-nodes")]
    public async Task<IActionResult> GetAllNodes()
    {
        try
        {
            var tasks = _nodes.Select<Uri, Task<object>>(async nodeUri =>
            {
                try
                {
                    string baseUrl = nodeUri.ToString().EndsWith("/") ? nodeUri.ToString() : nodeUri.ToString() + "/";
                    var requestUri = new Uri(baseUrl + "api/cache/all-items");

                    var response = await _httpClient.GetAsync(requestUri);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var cacheItems = JsonConvert.DeserializeObject<List<CacheItem>>(content);

                        return new
                        {
                            NodeUrl = baseUrl,
                            Items = cacheItems.Select(item => new
                            {
                                Key = item.Key,
                                Value = item.Value,
                                CreatedAt = item.CreatedAt,
                                LastAccessed = item.LastAccessed,
                                TTL = item.TTL,
                                IsExpired = item.IsExpired()
                            }),
                            Status = "Success"
                        };
                    }

                    return new
                    {
                        NodeUrl = baseUrl,
                        Items = new List<object>(),
                        Status = "Error",
                        Error = $"Status code: {(int)response.StatusCode}"
                    };
                }
                catch (Exception ex)
                {
                    return new
                    {
                        NodeUrl = nodeUri.ToString(),
                        Items = new List<object>(),
                        Status = "Error",
                        Error = ex.Message
                    };
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Status = "Critical Error",
                Error = ex.Message
            });
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