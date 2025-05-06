using ClusterManager.DTO;
using ClusterManager.Interfaces;
using Docker.DotNet.Models;
using Docker.DotNet;
using Node.DTO;

namespace ClusterManager.Services;

public class NodesService(HttpClient _httpClient) : INodesService
{
    private readonly object _lock = new();
    private readonly List<Uri> Nodes = new();

    public async Task<Result<string?>> SetCacheItemAsync(CacheItemDto item)
    {
        try
        {
            var nodeUrl = GetNodeUrlForItemKey(item.Key);
            string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
            var requestUri = new Uri(baseUrl + "api/cache");

            var response = await _httpClient.PutAsJsonAsync(requestUri, item);
            if (response.IsSuccessStatusCode)
            {
                return Result<string?>.Ok("Успешно.", 200);
            }
            else
            {
                return Result<string?>.Fail(await response.Content.ReadAsStringAsync(), (int)response.StatusCode);
            }
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
            var nodeUrl = GetNodeUrlForItemKey(key);
            string baseUrl = nodeUrl.ToString().EndsWith("/") ? nodeUrl.ToString() : nodeUrl.ToString() + "/";
            var requestUrl = new Uri(baseUrl + "api/cache/" + Uri.EscapeDataString(key));

            var response = await _httpClient.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Result<string?>.Ok(content, 200);
            }
            else
            {
                return Result<string?>.Fail("Элемент не найден", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            return Result<string?>.Fail(ex.Message, 500);
        }
    }
    public async Task<Result<NodeResponseDto>> CreateNodeAsync(string containerName)
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

                var response = await dockerClient.Containers.CreateContainerAsync(createParams);
                var started = await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                if (!started)
                {
                    return Result<NodeResponseDto>.Fail("Ошибка запуска контейнера. ID=" + response.ID, 500);
                }

                var inspect = await dockerClient.Containers.InspectContainerAsync(response.ID);
                var hostPort = inspect.NetworkSettings.Ports["8080/tcp"].FirstOrDefault()?.HostPort;
                var nodeUrl = "http://localhost:" + hostPort;

                NodeResponseDto dto = new()
                {
                    Url = nodeUrl,
                    Id = response.ID
                };
                if (!RegisterNode(nodeUrl))
                    return Result<NodeResponseDto>.Fail("Ошибка регистрации узла.", 500);

                return Result<NodeResponseDto>.Ok(dto, 200);
            }
        }
        catch (Exception ex)
        {
            return Result<NodeResponseDto>.Fail(ex.Message, 500);
        }
    }
    private NodeConfigurationDto GetNodeSettings(string containerName)
    {
        var defaultSettings = new NodeConfigurationDto
        {
            Image = "my-node-service:dev",
            ContainerName = "node-container-" + containerName + "-" + Guid.NewGuid(),
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
    private bool RegisterNode(string url)
    {
        lock (_lock)
        {
            var nodeUrl = new Uri(url.EndsWith("/") ? url : url + "/");
            if (!Nodes.Contains(nodeUrl))
            {
                Nodes.Add(nodeUrl);
            }
        }

        return true;
    }
    private Uri GetNodeUrlForItemKey(string key)
    {
        if (Nodes.Count == 0)
            throw new Exception();

        int hash = key.GetHashCode();
        int index = Math.Abs(hash) % Nodes.Count;
        return Nodes[index];
    }
}
