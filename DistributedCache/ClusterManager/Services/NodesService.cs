using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Docker.DotNet.Models;
using Docker.DotNet;
using Newtonsoft.Json;

namespace ClusterManager.Services;

public class NodesService(HttpClient _httpClient) : INodesService
{
    private readonly object _lock = new();
    private readonly List<List<Node>> Nodes = new();

    public async Task<Result<List<List<NodeWithDataResponseDto>>>> GetAllNodesWithDataAsync()
    {
        List<List<NodeWithDataResponseDto>> result = new();
        foreach (var copyNodes in Nodes)
        {
            List<NodeWithDataResponseDto> nodesWithData = new();
            foreach (var node in copyNodes)
            {
                string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
                var requestUri = new Uri(baseUrl + "api/cache/all");
                var response = await _httpClient.GetAsync(requestUri);

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Result<List<List<NodeWithDataResponseDto>>>.Fail(content, (int)response.StatusCode);
                }

                Console.WriteLine(content);
                var dto = JsonConvert.DeserializeObject<List<CacheItemResponseDto>>(content);
                NodeWithDataResponseDto nodeWithData = new()
                {
                    Url = node.Url,
                    Id = node.Id,
                    Items = dto
                };

                nodesWithData.Add(nodeWithData);
            }
            result.Add(nodesWithData);
        }

        return Result<List<List<NodeWithDataResponseDto>>>.Ok(result, 200);
    }
    public async Task<Result<NodeWithDataResponseDto>> GetNodeWithDataAsync(string key)
    {
        int index = GetNodesUrlListIndexForItemKey(key);

        var selectedNodes = Nodes[index];

        foreach (var node in selectedNodes)
        {
            string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
            var requestUri = new Uri(baseUrl + $"api/cache/{key}");
            var response = await _httpClient.GetAsync(requestUri);

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                continue; // пробуем следующую ноду
            }

            var dto = JsonConvert.DeserializeObject<CacheItemResponseDto>(content);

            if (dto == null)
                continue;

            NodeWithDataResponseDto nodeWithData = new()
            {
                Url = node.Url,
                Id = node.Id,
                Items = new List<CacheItemResponseDto> { dto }
            };

            return Result<NodeWithDataResponseDto>.Ok(nodeWithData, 200);
        }

        return Result<NodeWithDataResponseDto>.Fail("Key not found in selected node group", 404);
    }
    
    public async Task<Result<List<NodeStatusDto>>> GetAllNodeStatusesAsync()
    {
        List<NodeStatusDto> result = new();

        foreach (var nodeGroup in Nodes)
        {
            foreach (var node in nodeGroup)
            {
                string baseUrl = node.Url.ToString().EndsWith("/") ? node.Url.ToString() : node.Url.ToString() + "/";
                var healthUri = new Uri(baseUrl + "api/health"); // сделать health endpoint

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
        }

        return Result<List<NodeStatusDto>>.Ok(result, 200);
    }
    
    public async Task<Result<NodeStatusDto>> GetNodeStatusAsync(string key)
    {
        int index = GetNodesUrlListIndexForItemKey(key);
        var selectedNodes = Nodes[index];

        foreach (var node in selectedNodes)
        {
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

        return Result<NodeStatusDto>.Fail("No node found for key", 404);
    }


    public async Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item)
    {
        try
        {
            var nodesUrlIndex = GetNodesUrlListIndexForItemKey(item.Key);
            var nodes = Nodes[nodesUrlIndex];

            foreach(var node in nodes)
            {
                var url = node.Url;

                string baseUrl = url.ToString().EndsWith("/") ? url.ToString() : url.ToString() + "/";
                var requestUri = new Uri(baseUrl + "api/cache");
                var response = await _httpClient.PutAsJsonAsync(requestUri, item);

                if (!response.IsSuccessStatusCode)
                {
                    return Result<string?>.Fail(await response.Content.ReadAsStringAsync(), (int)response.StatusCode);
                }
            }

            return Result<string?>.Ok("Успешно.", 200);
        }
        catch (Exception ex)
        {
            return Result<string?>.Fail(ex.Message, 500);
        }
    }
    public async Task<Result<string?>> GetCacheItemAsync(string key)
    {
        try
        {
            var nodesUrlIndex = GetNodesUrlListIndexForItemKey(key);
            var nodes = Nodes[nodesUrlIndex];
            HttpResponseMessage? response = null;

            foreach (var node in nodes)
            {
                var url = node.Url;
                string baseUrl = url.ToString().EndsWith("/") ? url.ToString() : url.ToString() + "/";
                var requestUri = new Uri(baseUrl + "api/cache/" + Uri.EscapeDataString(key));
                response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }

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
    public async Task<Result<List<NodeDto>>> CreateNodeAsync(string containerName, int copiesCount)
    {
        var nodeSettings = GetNodeSettings(containerName);

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
                    createParams.Name = GenerateContainerName(containerName);
                    var response = await dockerClient.Containers.CreateContainerAsync(createParams);
                    var started = await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                    if (!started)
                    {
                        return Result<List<NodeDto>>.Fail("Ошибка запуска контейнера. ID=" + response.ID, 500);
                    }

                    var inspect = await dockerClient.Containers.InspectContainerAsync(response.ID);
                    var hostPort = inspect.NetworkSettings.Ports["8080/tcp"].FirstOrDefault()?.HostPort;
                    var nodeUrl = "http://localhost:" + hostPort;

                    NodeDto dto = new()
                    {
                        Url = nodeUrl,
                        Id = response.ID
                    };

                    nodesToRegister.Add(dto);;
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
    private bool RegisterNode(string id, string url)
    {
        lock (_lock)
        {
            Node node = new()
            {
                Id = id,
                Url = new Uri(url.EndsWith("/") ? url : url + "/")
            };

            Nodes.Add(new List<Node> { node });
        }

        return true;
    }
    private bool RegisterNode(List<NodeDto> nodes)
    {
        List<Node> nodesList = new();
        foreach (var nodeDto in nodes)
        {
            Node node = new()
            {
                Id = nodeDto.Id,
                Url = new Uri(nodeDto.Url.EndsWith("/") ? nodeDto.Url : nodeDto.Url + "/")
            };
            nodesList.Add(node);
        }

        lock (_lock)
        {
            Nodes.Add(nodesList);
        }

        return true;
    }
    private int GetNodesUrlListIndexForItemKey(string key)
    {
        if (Nodes.Count == 0)
            throw new Exception();

        int hash = key.GetHashCode();
        int index = Math.Abs(hash) % Nodes.Count;
        return index;
    }
}
