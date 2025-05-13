using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Docker.DotNet.Models;
using Docker.DotNet;
using Newtonsoft.Json;
using ClusterManager.Utils;
using System;

namespace ClusterManager.Services;

public class NodesService : INodesService
{
    private readonly HttpClient _httpClient;
    private readonly object _lock = new();
    private readonly SortedList<string, Node> _nodes = new();
    private readonly List<string> _sortedKeys;

    public NodesService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _sortedKeys = _nodes.Keys.ToList();
    }


    public async Task<Result<List<NodeWithDataResponseDto>>> GetAllNodesWithDataAsync()
    {
        List<NodeWithDataResponseDto> result = new();
        foreach (var node in _nodes.Values)
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
                    return Result<List<NodeWithDataResponseDto>>.Fail(ReplicaDataResult.Error,
                        ReplicaDataResult.StatusCode);
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

    public async Task<Result<NodeWithDataResponseDto>> GetNodeWithDataAsync(string key)
    {
        var index = GetNodeKeyForItemKey(key);

        var node = _nodes[index];

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

        foreach (var node in _nodes.Values)
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
        var index = GetNodeKeyForItemKey(key);
        var node = _nodes[index];

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
            var nodeKey = GetNodeKeyForItemKey(item.Key);
            var node = _nodes[nodeKey];

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
            return Result<HttpResponseMessage?>.Fail(await response.Content.ReadAsStringAsync(),
                (int)response.StatusCode);
        }

        return Result<HttpResponseMessage?>.Ok(response, (int)response.StatusCode);
    }

    public async Task<Result<string?>> GetCacheItemAsync(string key)
    {
        try
        {
            var nodeKey = GetNodeKeyForItemKey(key);
            var node = _nodes[nodeKey];
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

    public async Task<Result<List<NodeDto>>> CreateNodeAsync(string name, int copiesCount)
    {
        var nodeSettings = GetNodeSettings(name);

        var config = new NodeConfigurationDto
        {
            Image = nodeSettings.Image,
            ContainerName = nodeSettings.ContainerName,
            EnvironmentVariables = nodeSettings.EnvironmentVariables,
            PortBindings = nodeSettings.PortBindings
        };

        try
        {
            // Linux
            // var dockerUri = new Uri("unix:///var/run/docker.sock");

            // Windows
            var dockerUri = new Uri("npipe://./pipe/docker_engine");

            using (var dockerClient = new DockerClientConfiguration(dockerUri).CreateClient())
            {
                dockerClient.DefaultTimeout = TimeSpan.FromMinutes(5);

                var createParams = new CreateContainerParameters
                {
                    Image = config.Image,
                    Name = config.ContainerName,
                    Env = new List<string>(),
                    ExposedPorts = new Dictionary<string, EmptyStruct>(),
                    HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>()
                    }
                };

                if (config.EnvironmentVariables != null)
                {
                    foreach (var env in config.EnvironmentVariables)
                    {
                        createParams.Env.Add(env.Key + "=" + env.Value);
                    }
                }

                if (config.PortBindings != null)
                {
                    foreach (var binding in config.PortBindings)
                    {
                        createParams.ExposedPorts[binding.Key] = default;
                        createParams.HostConfig.PublishAllPorts = true; // публикуем внешний порт
                    }
                }

                List<NodeDto> nodesToRegister = new();
                for (int i = 0; i < copiesCount; i++)
                {
                    var containerName = GenerateContainerName(name);
                    createParams.Name = containerName;
                    var response = await dockerClient.Containers.CreateContainerAsync(createParams);
                    var started =
                        await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                    if (!started)
                    {
                        return Result<List<NodeDto>>.Fail("Ошибка запуска контейнера. ID=" + response.ID, 500);
                    }

                    var inspect = await dockerClient.Containers.InspectContainerAsync(response.ID);
                    var hostPort = inspect.NetworkSettings.Ports["8080/tcp"].FirstOrDefault()?.HostPort;
                    var nodeUrl = "http://localhost:" + hostPort;

                    NodeDto dto = new()
                    {
                        Name = containerName,
                        Url = nodeUrl,
                        Id = response.ID
                    };

                    nodesToRegister.Add(dto);
                    ;
                }

                var isRegisteredNode = RegisterNode(nodesToRegister);
                if (!isRegisteredNode)
                    return Result<List<NodeDto>>.Fail("Ошибка регистрации узлов.", 500);

                return Result<List<NodeDto>>.Ok(nodesToRegister, 200);
            }
        }
        catch (Exception ex)
        {
            return Result<List<NodeDto>>.Fail(ex.Message, 500);
        }
    }

    private NodeConfigurationDto GetNodeSettings(string containerName)
    {
        var defaultSettings = new NodeConfigurationDto
        {
            Image = "my-node:dev",
            ContainerName = GenerateContainerName(containerName),
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
            },
            PortBindings = new Dictionary<string, string>
            {
                { "8080/tcp", "" }
            }
        };

        return defaultSettings;
    }

    private string GenerateContainerName(string containerName)
    {
        return "node-container-" + containerName + "-" + Guid.NewGuid();
    }

    private bool RegisterNode(string id, string url, string name)
    {
        lock (_lock)
        {
            Node node = new()
            {
                Name = name,
                Id = id,
                Url = new Uri(url.EndsWith("/") ? url : url + "/")
            };

            var hashNode = HashGenerator.GetMd5HashString(name);

            _nodes.Add(hashNode, node);
        }

        return true;
    }

    private bool RegisterNode(List<NodeDto> nodes)
    {
        Node node = new()
        {
            Name = nodes[0].Name,
            Id = nodes[0].Id,
            Url = new Uri(nodes[0].Url.EndsWith("/") ? nodes[0].Url : nodes[0].Url + "/")
        };

        for (int i = 1; i < nodes.Count; i++)
        {
            Node replica = new()
            {
                Name = nodes[i].Name,
                Id = nodes[i].Id,
                Url = new Uri(nodes[i].Url.EndsWith("/") ? nodes[i].Url : nodes[i].Url + "/")
            };

            node.Replicas.Add(replica);
        }

        lock (_lock)
        {
            var hashNode = HashGenerator.GetMd5HashString(node.Name);

            _nodes.Add(hashNode, node);
            UpdateSortedKeys(hashNode);
        }

        return true;
    }
    private bool DeleteNode(Node node)//docker и репелики сделать
    {
        var hashNode = HashGenerator.GetMd5HashString(node.Name);

        if (_nodes.ContainsKey(hashNode))
        {
            lock (_lock)
            {
                _nodes.Remove(hashNode);
            }
        }

        return true;
    }

    private bool UpdateNode(List<NodeDto> nodes, Node node)
    {
        DeleteNode(node);
        RegisterNode(nodes);
        return true;
    }

    private void UpdateSortedKeys(string hashNode)
    {
        var idx = _sortedKeys.BinarySearch(hashNode, StringComparer.Ordinal);
        if (idx < 0)
            idx = ~idx;

        _sortedKeys.Insert(idx, hashNode);
    }

    private string GetNodeKeyForItemKey(string key)
    {
        if (_nodes.Count == 0)
            throw new InvalidOperationException("Нет доступных нод для кэширования.");

        var hash = HashGenerator.GetMd5HashString(key);

        int idx = _sortedKeys.BinarySearch(hash, StringComparer.Ordinal);

        if (idx < 0)
        {
            idx = ~idx;
            if (idx >= _sortedKeys.Count)
                idx = 0;
        }

        return _sortedKeys[idx];
    }
}