using ClusterManager.Common;
using ClusterManager.Common.Utils;
using ClusterManager.DTO;
using ClusterManager.Enums;
using ClusterManager.Interfaces;
using ClusterManager.Models;
using Newtonsoft.Json;

namespace ClusterManager.Services;

public class NodeManager : INodeManager
{
    private readonly IHttpService _httpService;
    private readonly ICacheStorage _cache;
    public NodeManager(IHttpService httpService, ICacheStorage cacheStorage)
    {
        _httpService = httpService;
        _cache = cacheStorage;
    }
    public async Task<Result<string>> RebalanceAfterDeletingNode(Node node)
    {
        var nodeName = node.Name;
        var nodeDataResult = await GetNodeDataAsync(node);
        if (!nodeDataResult.IsSuccess)
            return Result<string>.Fail(nodeDataResult.Error!, nodeDataResult.StatusCode);

        var nextNodeResult = _cache.GetNextNode(node.Name);
        if (!nextNodeResult.IsSuccess)
            return Result<string>.Fail(nextNodeResult.Error!, nextNodeResult.StatusCode);

        List<CacheItemRequestDto> nodeData = nodeDataResult.Data.Select(c => new CacheItemRequestDto
        {
            Key = c.Key,
            Value = c.Value,
            TTL = c.TTL
        }).ToList();

        await AddNodeAndReplicasDataAsync(nextNodeResult.Data, nodeData);

        return Result<string>.Ok("Успешная перебалансировка.", 200);
    }
    public async Task<Result<string>> RebalanceAfterCreatingNode(Node creatingNode)
    {
        var creatingNodeDataResult = await GetNodeDataAsync(creatingNode);
        if (!creatingNodeDataResult.IsSuccess)
            return Result<string>.Fail(creatingNodeDataResult.Error!, creatingNodeDataResult.StatusCode);
        var creatingNodeData = creatingNodeDataResult.Data;

        var nextNodeResult = _cache.GetNextNode(creatingNode.Name);
        if (!nextNodeResult.IsSuccess)
            return Result<string>.Fail(nextNodeResult.Error!, nextNodeResult.StatusCode);
        var nextNode = nextNodeResult.Data;

        var nextNodeDataResult = await GetNodeDataAsync(nextNode);
        if (!nextNodeDataResult.IsSuccess)
            return Result<string>.Fail(nextNodeDataResult.Error!, nextNodeDataResult.StatusCode);
        var nextNodeData = nextNodeDataResult.Data;

        List<CacheItemRequestDto> nodeDataToDelete = new();
        List<CacheItemRequestDto> nodeData = nextNodeData
            .Where(c => _cache.GetNodeKeyForItemKey(c.Key) == HashGenerator.GetMd5HashString(creatingNode.Name))
            .Select(c => new CacheItemRequestDto
            {
                Key = c.Key,
                Value = c.Value,
                TTL = c.TTL
            }).ToList();

        await AddNodeAndReplicasDataAsync(creatingNode, nodeData);
        await DeleteNodeAndReplicasDataAsync(nextNode, nodeData);

        return Result<string>.Ok("Успешная перебалансировка после добавления ноды.", 200);
    }
    public async Task<Result<List<NodeWithDataResponseDto>>> GetAllNodesWithDataAsync()
    {
        var nodes = _cache.Nodes;
        if (nodes == null || nodes.Count == 0)
            return Result<List<NodeWithDataResponseDto>>.Fail("Список нод пуст.", 404);
        List<NodeWithDataResponseDto> result = new();
        foreach (var node in nodes.Values)
        {
            var NodeDataResult = await GetNodeDataAsync(node);
            if (!NodeDataResult.IsSuccess || NodeDataResult.Data == null)
            {
                return Result<List<NodeWithDataResponseDto>>.Fail(NodeDataResult.Error, NodeDataResult.StatusCode);
            }

            NodeWithDataResponseDto nodeWithData = new()
            {
                Name = node.Name,
                Status = node.Status,
                Url = node.Url,
                Id = node.Id,
                Items = NodeDataResult.Data
            };

            foreach (var replica in node.Replicas)
            {
                var ReplicaDataResult = await GetNodeDataAsync(replica);
                if (!ReplicaDataResult.IsSuccess || ReplicaDataResult.Data == null)
                {
                    return Result<List<NodeWithDataResponseDto>>.Fail(ReplicaDataResult.Error, ReplicaDataResult.StatusCode);
                }

                NodeWithDataResponseDto replicaWithData = new()
                {
                    Name = replica.Name,
                    Status = node.Status,
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

    private async Task<Result<List<CacheItemResponseDto>>> GetNodeDataAsync(Node node)
    {
        var responseResult = await _httpService.SendRequestAsync<object>(node.Url.ToString(), "api/cache/all", HttpMethodEnum.Get);
        if (!responseResult.IsSuccess)
            return Result<List<CacheItemResponseDto>>.Fail(string.IsNullOrEmpty(responseResult.Error) ? "Ошибка получения данных узла." : responseResult.Error, responseResult.StatusCode);
        var response = responseResult.Data;
        var content = await response!.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Result<List<CacheItemResponseDto>>.Fail(content, (int)response.StatusCode);
        }

        var dto = JsonConvert.DeserializeObject<List<CacheItemResponseDto>>(content);

        return Result<List<CacheItemResponseDto>>.Ok(dto, 200);
    }
    private async Task<Result<string>> AddNodeAndReplicasDataAsync(Node node, CacheItemRequestDto item)
    {
        var responseResult = await _httpService.SendRequestAsync<CacheItemRequestDto>(node.Url.ToString(), "api/cache/single", HttpMethodEnum.Put, item);
        if (!responseResult.IsSuccess)
            return Result<string>.Fail(string.IsNullOrEmpty(responseResult.Error) ? "Ошибка добавления данных в узел." : responseResult.Error, responseResult.StatusCode);

        foreach (var replica in node.Replicas)
        {
            var responseReplicaResult = await _httpService.SendRequestAsync<CacheItemRequestDto>(replica.Url.ToString(), "api/cache/single", HttpMethodEnum.Put, item);
            if (!responseReplicaResult.IsSuccess)
                return Result<string>.Fail(string.IsNullOrEmpty(responseReplicaResult.Error) ? "Ошибка добавления данных в узел." : responseReplicaResult.Error, responseReplicaResult.StatusCode);
        }


        return Result<string>.Ok("Успешно.", 200);
    }
    private async Task<Result<string>> AddNodeAndReplicasDataAsync(Node node, List<CacheItemRequestDto> items)
    {
        var responseResult = await _httpService.SendRequestAsync<List<CacheItemRequestDto>>(node.Url.ToString(), "api/cache/multiple", HttpMethodEnum.Put, items);
        if (!responseResult.IsSuccess)
            return Result<string>.Fail(string.IsNullOrEmpty(responseResult.Error) ? "Ошибка добавления данных в узел." : responseResult.Error, responseResult.StatusCode);

        foreach (var replica in node.Replicas)
        {
            var responseReplicaResult = await _httpService.SendRequestAsync<List<CacheItemRequestDto>>(replica.Url.ToString(), "api/cache/multiple", HttpMethodEnum.Put, items);
            if (!responseReplicaResult.IsSuccess)
                return Result<string>.Fail(string.IsNullOrEmpty(responseReplicaResult.Error) ? "Ошибка добавления данных в узел." : responseReplicaResult.Error, responseReplicaResult.StatusCode);
        }

        return Result<string>.Ok("Успешно.", 200);
    }
    private async Task<Result<string>> DeleteNodeAndReplicasDataAsync(Node node, List<CacheItemRequestDto> itemsToDelete)
    {
        var responseResult = await _httpService.SendRequestAsync<List<CacheItemRequestDto>>(node.Url.ToString(), "api/cache/delete/multiple", HttpMethodEnum.Post, itemsToDelete);
        if (!responseResult.IsSuccess)
            return Result<string>.Fail(string.IsNullOrEmpty(responseResult.Error) ? "Ошибка добавления данных в узел." : responseResult.Error, responseResult.StatusCode);

        foreach (var replica in node.Replicas)
        {
            var responseReplicaResult = await _httpService.SendRequestAsync<List<CacheItemRequestDto>>(replica.Url.ToString(), "api/cache/delete/multiple", HttpMethodEnum.Post, itemsToDelete);
            if (!responseReplicaResult.IsSuccess)
                return Result<string>.Fail(string.IsNullOrEmpty(responseReplicaResult.Error) ? "Ошибка добавления данных в узел." : responseReplicaResult.Error, responseReplicaResult.StatusCode);
        }

        return Result<string>.Ok("Успешно.", 200);
    }
    public async Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item)
    {
        try
        {
            var nodeKey = _cache.GetNodeKeyForItemKey(item.Key);
            var node = _cache.Nodes[nodeKey];

            var nodeSetResult = await AddNodeAndReplicasDataAsync(node, item);

            if (!nodeSetResult.IsSuccess)
            {
                return Result<string?>.Fail(nodeSetResult.Error, nodeSetResult.StatusCode);

            }
            foreach (var replica in node.Replicas)
            {
                var replicaSetResult = await AddNodeAndReplicasDataAsync(replica, item);

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
        var responseResult = await _httpService.SendRequestAsync<object>(node.Url.ToString(), "api/cache/" + Uri.EscapeDataString(key), HttpMethodEnum.Get);
        if (!responseResult.IsSuccess)
            return Result<HttpResponseMessage?>.Fail(string.IsNullOrEmpty(responseResult.Error) ? "Ошибка получения данных узла." : responseResult.Error, responseResult.StatusCode);

        var response = responseResult.Data;

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
}
