using ClusterManager.DTO;
using Docker.DotNet.Models;
using Docker.DotNet;
using ClusterManager.Interfaces;
using ClusterManager.Models;
using ClusterManager.Common;
using ClusterManager.Enums;

namespace ClusterManager.Services;

public class NodeRegistry : INodeRegistry
{
    private readonly ICacheStorage _cache;
    private readonly INodeManager _manager;
    //private readonly Uri _dockerUri = new Uri("npipe://./pipe/docker_engine"); // Windows
    private readonly Uri _dockerUri = new Uri("unix:///var/run/docker.sock"); // Linux
    //private const string baseUrl = "http://localhost:";
    public NodeRegistry(ICacheStorage cacheStorage, INodeManager manager)
    {
        _cache = cacheStorage;
        _manager = manager;
    }
    public async Task<Result<List<string>>> DeleteNodeByNameAsync(string name)
    {
        var node = _cache.GetNodeByName(name);

        await _manager.RebalanceAfterDeletingNode(node);

        List<(string name, string id)> containers = new();
        containers.Add((node.Name, node.Id));
        foreach (var rep in node.Replicas)
        {
            containers.Add((rep.Name, rep.Id));
        }
        List<string> failedToRemoveNames = new();

        using (var dockerClient = new DockerClientConfiguration(_dockerUri).CreateClient())
        {
            dockerClient.DefaultTimeout = TimeSpan.FromMinutes(5);

            var myLock = new Object();
            var tasks = new List<Task>();

            foreach (var container in containers)
            {
                tasks.Add(RemoveContainerAsync(dockerClient, container.name, container.id, failedToRemoveNames, myLock));
                _cache.RemoveMasterWithReplicas(container.name);
            }

            await Task.WhenAll(tasks);
        }

        if (failedToRemoveNames.Count > 0)
            return Result<List<string>>.Fail("Ошибка удаления узлов. Название узлов, которые не удалились: " + string.Join(", ", failedToRemoveNames), 500);

        return Result<List<string>>.Ok(failedToRemoveNames, 200);
    }
    public async Task<Result<List<string>>> ForceDeleteNodeByNameAsync(string name)
    {
        var node = _cache.GetNodeByName(name);
        List<(string name, string id)> containers = new();
        containers.Add((node.Name, node.Id));
        foreach(var rep in node.Replicas)
        {
            containers.Add((rep.Name, rep.Id));
        }
        List<string> failedToRemoveNames = new();

        using (var dockerClient = new DockerClientConfiguration(_dockerUri).CreateClient())
        {
            dockerClient.DefaultTimeout = TimeSpan.FromMinutes(5);

            var myLock = new Object();
            var tasks = new List<Task>();

            foreach (var container in containers)
            {
                tasks.Add(RemoveContainerAsync(dockerClient, container.name, container.id, failedToRemoveNames, myLock));
                _cache.RemoveMasterWithReplicas(container.name);
            }

            await Task.WhenAll(tasks);
        }

        if (failedToRemoveNames.Count > 0)
            return Result<List<string>>.Fail("Ошибка удаления узлов. Название узлов, которые не удалились: " + string.Join(", ", failedToRemoveNames), 500);

        return Result<List<string>>.Ok(failedToRemoveNames, 200);
    }
    private async Task RemoveContainerAsync(DockerClient dockerClient, string containerName, string containerId, List<string> failedToRemoveNames, object myLock)
    {
        try
        {
            await dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true,
                    RemoveVolumes = true
                });
        }
        catch (DockerContainerNotFoundException)
        {
            Console.WriteLine($"Контейнер {containerId} не найден.");
            lock (myLock)
            {
                failedToRemoveNames.Add(containerName);
            }
        }
        catch (DockerApiException ex)
        {
            Console.WriteLine($"Ошибка API Docker: {ex.Message}");
            lock (myLock)
            {
                failedToRemoveNames.Add(containerName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Неизвестная ошибка при удалении контейнера " + containerName + ":" + ex.Message);
            lock (myLock)
            {
                failedToRemoveNames.Add(containerName);
            }
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
            using (var dockerClient = new DockerClientConfiguration(_dockerUri).CreateClient())
            {
                dockerClient.DefaultTimeout = TimeSpan.FromMinutes(5);

                var createParams = new CreateContainerParameters
                {
                    Image = config.Image,
                    Env = new List<string>(),
                    ExposedPorts = new Dictionary<string, EmptyStruct>(),
                    HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>(),
                        NetworkMode = "cache-net"
                    },
                    Labels = new Dictionary<string, string>
                    {
                        { "app", "distributedCache" },
                        { "masterName", "" },
                        { "isReplica", "" }
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

                List<NodeDto> masterNodesToRegister = new();
                List<NodeDto> replicasToRegister = new();

                var masterNodesToRegisterLock = new Object();
                var replicasToRegisterLock = new Object();

                var tasks = new List<Task>();

                tasks.Add(CreateContainerAsync(dockerClient, config.ContainerName, createParams, masterNodesToRegister, masterNodesToRegisterLock, true));
                for (int i = 0; i < copiesCount-1; i++)
                {
                    string containerName = GenerateContainerName(name);
                    tasks.Add(CreateContainerAsync(dockerClient, containerName, createParams, replicasToRegister, replicasToRegisterLock, false, config.ContainerName));
                }

                await Task.WhenAll(tasks);
                
                var isRegisteredNode = RegisterNode(masterNodesToRegister, replicasToRegister);
                if (!isRegisteredNode)
                    return Result<List<NodeDto>>.Fail("Ошибка регистрации узлов.", 500);

                List<NodeDto> allRegisteredNodes = masterNodesToRegister.Concat(replicasToRegister).ToList();
                return Result<List<NodeDto>>.Ok(allRegisteredNodes, 200);
            }
        }
        catch (Exception ex)
        {
            return Result<List<NodeDto>>.Fail(ex.Message, 500);
        }
    }
    private async Task CreateContainerAsync(DockerClient dockerClient, string containerName, CreateContainerParameters createParams, List<NodeDto> nodesToRegister, object myLock, bool isMaster, string? masterName = null)
    {
        try
        {
            createParams.Name = containerName;

            if (isMaster)
            {
                createParams.Labels["isReplica"] = "false";
                createParams.Labels["masterName"] = containerName;
            }
            else
            {
                createParams.Labels["isReplica"] = "true";
                createParams.Labels["masterName"] = masterName;
            }

            var response = await dockerClient.Containers.CreateContainerAsync(createParams);
            var started = await dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
            if (!started)
            {
                Console.WriteLine("Ошибка запуска контейнера. ID=" + response.ID);
            }

            var inspect = await dockerClient.Containers.InspectContainerAsync(response.ID);
            var hostPort = inspect.NetworkSettings.Ports["8080/tcp"].FirstOrDefault()?.HostPort;
            //var nodeUrl = baseUrl + hostPort;
            var nodeUrl = "http://" + containerName + ":" + 8080;

            NodeDto dto = new()
            {
                Name = containerName,
                Url = nodeUrl,
                Id = response.ID
            };

            lock (myLock)
            {
                nodesToRegister.Add(dto);
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine("Ошибка создания контейнера: " + ex.Message);
        }
    }
    private NodeConfigurationDto GetNodeSettings(string name)
    {
        var defaultSettings = new NodeConfigurationDto
        {
            Image = "my-node:dev",
            ContainerName = GenerateContainerName(name),
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
            },
            PortBindings = new Dictionary<string, string>
            {
                { "8080/tcp", "0" }
            }
        };

        return defaultSettings;
    }
    private string GenerateContainerName(string name)
    {
        return "node-container-" + name + "-" + Guid.NewGuid();
    }
    private bool RegisterNode(List<NodeDto> masterNodeDtoList, List<NodeDto> replicas)
    {
        List<Node> masterNodes = new();
        foreach(var m in masterNodeDtoList)
        {
            Node node = new()
            {
                Name = m.Name,
                Status = NodeStatusEnum.Initializing,
                Id = m.Id,
                Url = new Uri(m.Url.EndsWith("/") ? m.Url : m.Url + "/")
            };

            masterNodes.Add(node);
        }

        for (int i = 0; i < replicas.Count; i++)
        {
            Node replica = new()
            {
                Name = replicas[i].Name,
                Status = NodeStatusEnum.Initializing,
                Id = replicas[i].Id,
                Url = new Uri(replicas[i].Url.EndsWith("/") ? replicas[i].Url : replicas[i].Url + "/")
            };

            foreach(var masterNode in masterNodes)
            {
                masterNode.Replicas.Add(replica);
            }
        }

        foreach (var masterNode in masterNodes)
        {
            _cache.AddNode(masterNode.Name, masterNode);
            _manager.RebalanceAfterCreatingNode(masterNode);
        }
        return true;
    }
}
