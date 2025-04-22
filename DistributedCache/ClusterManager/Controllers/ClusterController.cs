using System.Diagnostics;
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
    private readonly List<Uri> _spareNodes = new();
    private readonly object _lock = new();
    private readonly HttpClient _httpClient;

    private const int MaxActiveNodes = 3;

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
            if (_nodes.Contains(nodeUri) || _spareNodes.Contains(nodeUri))
            {
                return Conflict("Node already registered");
            }

            if (_nodes.Count <= MaxActiveNodes)
            {
                _nodes.Add(nodeUri);
            }
            else
            {
                _spareNodes.Add(nodeUri);
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


    [HttpGet("nodes/status")]
    public async Task<IActionResult> GetNodesStatus()
    {
        try
        {
            if (_nodes.Count == 0)
            {
                return NotFound("No nodes registered in the cluster");
            }

            var nodeStatuses = new List<object>();

            foreach (var nodeUrl in _nodes)
            {
                object nodeInfo;
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
                    var requestUri = new Uri(baseUrl + "api/cache/health");

                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var response = await _httpClient.GetAsync(requestUri, timeoutCts.Token);

                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        var healthData = await response.Content.ReadFromJsonAsync<HealthResponse>();
                        nodeInfo = new
                        {
                            Url = nodeUrl,
                            LastChecked = DateTime.UtcNow,
                            Status = "Active",
                            ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                            ItemsCount = healthData?.ItemsCount,
                            Error = (string)null
                        };
                    }
                    else
                    {
                        nodeInfo = new
                        {
                            Url = nodeUrl,
                            LastChecked = DateTime.UtcNow,
                            Status = "Unhealthy",
                            ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                            ItemsCount = (int?)null,
                            Error = $"HTTP {(int)response.StatusCode}"
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    nodeInfo = new
                    {
                        Url = nodeUrl,
                        LastChecked = DateTime.UtcNow,
                        Status = "Inactive",
                        ResponseTimeMs = (double?)null,
                        ItemsCount = (int?)null,
                        Error = "Request timeout (2s)"
                    };
                }
                catch (Exception ex)
                {
                    nodeInfo = new
                    {
                        Url = nodeUrl,
                        LastChecked = DateTime.UtcNow,
                        Status = "Inactive",
                        ResponseTimeMs = (double?)null,
                        ItemsCount = (int?)null,
                        Error = ex.GetBaseException().Message
                    };
                }

                nodeStatuses.Add(nodeInfo);
            }

            // Создаем результат с явным приведением типов
            var activeNodes = nodeStatuses.Where(n => (string)((dynamic)n).Status == "Active");
            var inactiveNodes = nodeStatuses.Where(n => (string)((dynamic)n).Status != "Active");

            var result = new
            {
                ActiveNodes = activeNodes,
                InactiveNodes = inactiveNodes,
                TotalNodes = _nodes.Count,
                CheckedAt = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("node-info/{key}")]
    public async Task<IActionResult> GetNodeInfoByKey(string key)
    {
        try
        {
            if (_nodes.Count == 0)
            {
                return NotFound(new { Error = "No nodes available in cluster" });
            }

            // 1. Определяем ноду, ответственную за ключ
            var nodeUrl = GetNodeForItemKey(key);
            int nodeIndex = Math.Abs(key.GetHashCode()) % _nodes.Count;

            // 2. Собираем базовую информацию о ноде
            var nodeInfo = new
            {
                ResponsibleNode = new
                {
                    Url = nodeUrl.ToString(),
                    Index = nodeIndex,
                    IsLocal = nodeUrl.ToString().Contains(Request.Host.Value) // Проверяем локальная ли это нода
                },
                KeyInfo = new
                {
                    OriginalKey = key,
                    HashedKey = key.GetHashCode(),
                    KeySlot = Math.Abs(key.GetHashCode()) % _nodes.Count
                },
                ClusterInfo = new
                {
                    TotalNodes = _nodes.Count,
                    AllNodes = _nodes.Select(u => u.ToString()).ToList()
                }
            };

            // 3. Получаем детали кеша с целевой ноды (если доступна)
            object cacheItemInfo = null;
            try
            {
                string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
                var requestUri = new Uri(baseUrl + $"api/cache/item/{Uri.EscapeDataString(key)}");

                var response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    cacheItemInfo = await response.Content.ReadFromJsonAsync<object>();
                }
            }
            catch (Exception ex)
            {
                cacheItemInfo = new { Error = $"Failed to retrieve item from node: {ex.Message}" };
            }

            // 4. Формируем итоговый ответ
            var result = new
            {
                Metadata = new
                {
                    Timestamp = DateTime.UtcNow,
                    RequestedFrom = $"{Request.Scheme}://{Request.Host}"
                },
                NodeInfo = nodeInfo,
                CacheItem = cacheItemInfo
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }

    [HttpGet("all-nodes")]
    public async Task<IActionResult> GetAllNodes()
    {
        try
        {
            if (_nodes.Count == 0)
            {
                return NotFound("No nodes available");
            }

            var nodeResponses = new List<object>();
            foreach (var nodeUrl in _nodes)
            {
                try
                {
                    string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
                    var requestUri = new Uri(baseUrl + "api/cache/items");

                    var response = await _httpClient.GetAsync(requestUri);

                    var responseData = new
                    {
                        NodeUrl = nodeUrl,
                        StatusCode = response.StatusCode,
                        Items = response.IsSuccessStatusCode
                            ? await response.Content.ReadFromJsonAsync<Dictionary<string, object>>()
                            : null,
                        Error = !response.IsSuccessStatusCode
                            ? await response.Content.ReadAsStringAsync()
                            : null
                    };

                    nodeResponses.Add(responseData);
                }
                catch (HttpRequestException httpEx)
                {
                    nodeResponses.Add(new
                    {
                        NodeUrl = nodeUrl,
                        StatusCode = 503,
                        Error = $"Node unreachable: {httpEx.Message}"
                    });
                }
            }

            return Ok(nodeResponses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
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

    private void SwapNodesAsync(Uri nodeUri)
    {
        _nodes[_spareNodes.IndexOf(nodeUri)] = _spareNodes[_spareNodes.IndexOf(nodeUri)];
    }
}