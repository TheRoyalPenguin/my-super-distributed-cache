using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Newtonsoft.Json;

namespace ClusterManager.Services;

public class NodeManager : INodeManager
{
    private readonly HttpClient _httpClient;
    private readonly ICacheStorage _cache;
    public NodeManager(HttpClient httpClient, INodeRegistry nodeRegistry, ICacheStorage cacheStorage)
    {
        _httpClient = httpClient;
        _cache = cacheStorage;
    }

    public async Task<Result<List<NodeWithDataResponseDto>>> GetAllNodesWithDataAsync()
    {
        var nodes = _cache.Nodes;
        if (nodes == null || nodes.Count == 0)
            return Result<List<NodeWithDataResponseDto>>.Fail("Список нод пуст.", 404);
        List<NodeWithDataResponseDto> result = new();
        foreach (var node in nodes.Values)
        {
            var NodeDataResult = await GetNodeData(node);
            if (!NodeDataResult.IsSuccess || NodeDataResult.Data == null)
            {
                return Result<List<NodeWithDataResponseDto>>.Fail(NodeDataResult.Error, NodeDataResult.StatusCode);
            }

            NodeWithDataResponseDto nodeWithData = new()
            {
                Name = node.Name,
                Url = node.Url,
                Id = node.Id,
                Items = NodeDataResult.Data
            };

            foreach (var replica in node.Replicas)
            {
                var ReplicaDataResult = await GetNodeData(replica);
                if (!ReplicaDataResult.IsSuccess || ReplicaDataResult.Data == null)
                {
                    return Result<List<NodeWithDataResponseDto>>.Fail(ReplicaDataResult.Error, ReplicaDataResult.StatusCode);
                }

                NodeWithDataResponseDto replicaWithData = new()
                {
                    Name = replica.Name,
                    Url = replica.Url,
                    Id = replica.Id,
                    Items = ReplicaDataResult.Data
                };

                nodeWithData.Replicas.Add(replicaWithData);
            }
            result.Add(nodeWithData);
        }

        return Result<List<NodeWithDataResponseDto>>.Ok(result, 200);
    }

    private async Task<Result<List<CacheItemResponseDto>>> GetNodeData(Node node)
    {
        string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
        var requestUri = new Uri(baseUrl + "api/cache/all");
        var response = await _httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Result<List<CacheItemResponseDto>>.Fail(content, (int)response.StatusCode);
        }

        var dto = JsonConvert.DeserializeObject<List<CacheItemResponseDto>>(content);

        return Result<List<CacheItemResponseDto>>.Ok(dto, 200);
    }
    private async Task<Result<string>> SetNodeData(Node node, CacheItemRequestDto item)
    {
        var url = node.Url;

        string baseUrl = url.ToString().EndsWith("/") ? url.ToString() : url.ToString() + "/";
        var requestUri = new Uri(baseUrl + "api/cache");
        var response = await _httpClient.PutAsJsonAsync(requestUri, item);

        if (!response.IsSuccessStatusCode)
        {
            return Result<string>.Fail(await response.Content.ReadAsStringAsync(), (int)response.StatusCode);
        }

        return Result<string>.Ok("Успешно.", 200);
    }

    public async Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item)
    {
        try
        {
            var nodeKey = _cache.GetNodeKeyForItemKey(item.Key);
            var node = _cache.Nodes[nodeKey];

            var nodeSetResult = await SetNodeData(node, item);

            if (!nodeSetResult.IsSuccess)
            {
                return Result<string?>.Fail(nodeSetResult.Error, nodeSetResult.StatusCode);

            }
            foreach (var replica in node.Replicas)
            {
                var replicaSetResult = await SetNodeData(replica, item);

                if (!replicaSetResult.IsSuccess)
                {
                    return Result<string?>.Fail(replicaSetResult.Error, replicaSetResult.StatusCode);
                }
            }

            return Result<string?>.Ok("Успешно.", 200);
        }
        catch (Exception ex)
        {
            return Result<string?>.Fail(ex.Message, 500);
        }
    }
    private async Task<Result<HttpResponseMessage?>> GetCacheItem(Node node, string key)
    {
        var url = node.Url;
        string baseUrl = url.ToString().EndsWith("/") ? url.ToString() : url.ToString() + "/";
        var requestUri = new Uri(baseUrl + "api/cache/" + Uri.EscapeDataString(key));
        var response = await _httpClient.GetAsync(requestUri);

        if (!response.IsSuccessStatusCode)
        {
            return Result<HttpResponseMessage?>.Fail(await response.Content.ReadAsStringAsync(), (int)response.StatusCode);
        }

        return Result<HttpResponseMessage?>.Ok(response, (int)response.StatusCode);
    }
    public async Task<Result<string?>> GetCacheItemAsync(string key)
    {
        try
        {
            var nodeKey = _cache.GetNodeKeyForItemKey(key);
            var node = _cache.Nodes[nodeKey];
            HttpResponseMessage? response = null;

            var result = await GetCacheItem(node, key);

            if (!result.IsSuccess)
            {
                return Result<string?>.Fail(result.Error, result.StatusCode);
            }

            response = result.Data;

            if (response == null || !response.IsSuccessStatusCode)
            {
                return Result<string?>.Fail("Элемент не найден", response != null ? (int)response.StatusCode : 404);
            }

            var content = await response.Content.ReadAsStringAsync();
            return Result<string?>.Ok(content, 200);
        }
        catch (Exception ex)
        {
            return Result<string?>.Fail(ex.Message, 500);
        }
    }
    public async Task<Result<NodeWithDataResponseDto>> GetNodeWithDataAsync(string key)
    {
        var index = _cache.GetNodeKeyForItemKey(key);

        var node = _cache.Nodes[index];

        string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
        var requestUri = new Uri(baseUrl + $"api/cache/{key}");
        var response = await _httpClient.GetAsync(requestUri);

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Result<NodeWithDataResponseDto>.Fail("Key not found in selected node group",
                (int)response.StatusCode);
        }

        var dto = JsonConvert.DeserializeObject<CacheItemResponseDto>(content);


        NodeWithDataResponseDto nodeWithData = new()
        {
            Url = node.Url,
            Id = node.Id,
            Items = new List<CacheItemResponseDto> { dto }
        };

        return Result<NodeWithDataResponseDto>.Ok(nodeWithData, 200);
    }

    public async Task<Result<List<NodeStatusDto>>> GetAllNodeStatusesAsync()
    {
        List<NodeStatusDto> result = new();

        foreach (var node in _cache.Nodes.Values)
        {
            string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
            var healthUri = new Uri(baseUrl + "api/health");

            try
            {
                var response = await _httpClient.GetAsync(healthUri);
                bool isOnline = response.IsSuccessStatusCode;

                result.Add(new NodeStatusDto
                {
                    NodeId = node.Id,
                    Url = node.Url.ToString(),
                    IsOnline = isOnline,
                    StatusCode = (int)response.StatusCode
                });
            }
            catch
            {
                result.Add(new NodeStatusDto
                {
                    NodeId = node.Id,
                    Url = node.Url.ToString(),
                    IsOnline = false,
                    StatusCode = 0
                });
            }
        }

        return Result<List<NodeStatusDto>>.Ok(result, 200);
    }

    public async Task<Result<NodeStatusDto>> GetNodeStatusAsync(string key)
    {
        var index = _cache.GetNodeKeyForItemKey(key);
        var node = _cache.Nodes[index];

        string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
        var healthUri = new Uri(baseUrl + "api/health");

        try
        {
            var response = await _httpClient.GetAsync(healthUri);
            bool isOnline = response.IsSuccessStatusCode;

            return Result<NodeStatusDto>.Ok(new NodeStatusDto
            {
                NodeId = node.Id,
                Url = node.Url.ToString(),
                IsOnline = isOnline,
                StatusCode = (int)response.StatusCode
            }, 200);
        }
        catch
        {
            return Result<NodeStatusDto>.Ok(new NodeStatusDto
            {
                NodeId = node.Id,
                Url = node.Url.ToString(),
                IsOnline = false,
                StatusCode = 0
            }, 200);
        }
    }
}
