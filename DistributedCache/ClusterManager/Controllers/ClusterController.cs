using ClusterManager.DTO;
using ClusterManager.Services;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;
using Node.DTO;

namespace ClusterManager.Controllers;

[Route("api/cluster")]
[ApiController]
public class ClusterController(INodesStorage _nodesStorage, HttpClient _httpClient) : ControllerBase
{
    private readonly object _lock = new();

    [HttpPost("register")]
    public IActionResult RegisterNode([FromBody] NodeRegistrationRequest request)
    {
        lock (_lock)
        {
            var nodeUrl = new Uri(request.Url.EndsWith("/") ? request.Url : request.Url + "/");
            if (!_nodesStorage.Nodes.Contains(nodeUrl))
            {
                _nodesStorage.Nodes.Add(nodeUrl);
            }
        }

        return Ok();
    }

    [HttpGet("cache/{key}")]
    public async Task<IActionResult> GetCacheItem(string key)
    {
        try
        {
            var nodeUrl = GetNodeUrlForItemKey(key);
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

    [HttpPut("cache/{key}")]
    public async Task<IActionResult> SetCacheItem(string key, [FromBody] CacheItemDto item)
    {
        try
        {
            var nodeUrl = GetNodeUrlForItemKey(key);
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

    private Uri GetNodeUrlForItemKey(string key)
    {
        if (_nodesStorage.Nodes.Count == 0)
            throw new Exception();

        int hash = key.GetHashCode();
        int index = Math.Abs(hash) % _nodesStorage.Nodes.Count;
        return _nodesStorage.Nodes[index];
    }

    [HttpPost("nodes/create/{containerName}")]
    public async Task<IActionResult> CreateNode(string containerName)
    {
        var nodeSettings = GetNodeSettings(containerName);

        var config = new NodeConfigurationDTO
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
                        createParams.HostConfig.PortBindings[binding.Key] = new List<PortBinding>
                        {
                            new PortBinding
                            {
                                HostPort = binding.Value
                            }
                        };
                    }
                }

                var response = await dockerClient.Containers.CreateContainerAsync(createParams);
                var started = await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
                if (!started)
                {
                    return StatusCode(500, "Не удалось запустить контейнер");
                }

                return Ok(response.ID);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    private NodeConfigurationDTO GetNodeSettings(string containerName)
    {
        var defaultSettings = new NodeConfigurationDTO
        {
            Image = "my-node-service:dev",
            ContainerName = "node-container-" + containerName + "-" + Guid.NewGuid(),
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "NODE_ENV", "production" },
                { "CUSTOM_CONFIG", "значение" }
            },
            PortBindings = new Dictionary<string, string>
            {
                { "8080/tcp", "" }
            }
        };

        return defaultSettings;
    }
}
