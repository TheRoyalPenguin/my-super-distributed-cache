using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Docker.DotNet.Models;
using Docker.DotNet;
using Node.DTO;

namespace ClusterManager.Services;

public class NodesService(HttpClient _httpClient) : INodesService
{
    private readonly object _lock = new();
    private readonly List<List<Uri>> Nodes = new();

    public async Task<Result<string?>> SetCacheItemAsync(CacheItemDto item)
    {
        try
        {
            var nodesUrlIndex = GetNodesUrlListIndexForItemKey(item.Key);
            var nodesUrl = Nodes[nodesUrlIndex];

            foreach(var url in nodesUrl)
            {
                string baseUrl = url.ToString().EndsWith("/") ? url.ToString() : url.ToString() + "/";
                var requestUri = new Uri(baseUrl + "api/cache/");
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
            var nodesUrl = Nodes[nodesUrlIndex];
            HttpResponseMessage? response = null;

            foreach (var url in nodesUrl)
            {
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
    public async Task<Result<List<NodeResponseDto>>> CreateNodeAsync(string containerName, int copiesCount)
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

                List<NodeResponseDto> result = new();
                for (int i = 0; i < copiesCount; i++)
                {
                    createParams.Name = GenerateContainerName(containerName);
                    var response = await dockerClient.Containers.CreateContainerAsync(createParams);
                    var started = await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                    if (!started)
                    {
                        return Result<List<NodeResponseDto>>.Fail("Ошибка запуска контейнера. ID=" + response.ID, 500);
                    }

                    var inspect = await dockerClient.Containers.InspectContainerAsync(response.ID);
                    var hostPort = inspect.NetworkSettings.Ports["8080/tcp"].FirstOrDefault()?.HostPort;
                    var nodeUrl = "http://localhost:" + hostPort;

                    NodeResponseDto dto = new()
                    {
                        Url = nodeUrl,
                        Id = response.ID
                    };

                    var isRegisteredNode = RegisterNode(new List<string> { nodeUrl, nodeUrl });

                    if (!isRegisteredNode)
                        return Result<List<NodeResponseDto>>.Fail("Ошибка регистрации узла.", 500);

                    result.Add(dto);
                }

                return Result<List<NodeResponseDto>>.Ok(result, 200);
            }
        }
        catch (Exception ex)
        {
            return Result<List<NodeResponseDto>>.Fail(ex.Message, 500);
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
    private bool RegisterNode(string url)
    {
        lock (_lock)
        {
            var nodeUrl = new Uri(url.EndsWith("/") ? url : url + "/");
            Nodes.Add(new List<Uri> { nodeUrl });
        }

        return true;
    }
    private bool RegisterNode(List<string> urls)
    {
        List<Uri> uriList = new();
        foreach (var url in urls)
        {
            var nodeUrl = new Uri(url.EndsWith("/") ? url : url + "/");
            uriList.Add(nodeUrl);
        }

        lock (_lock)
        {
            Nodes.Add(uriList);
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
