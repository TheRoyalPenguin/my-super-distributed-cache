using ClusterManager.DTO;
using ClusterManager.Enums;
using ClusterManager.Interfaces;
using ClusterManager.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace ClusterManager.Services.BackgroundServices;

public class NodeRestoreService : BackgroundService
{
    private readonly ICacheStorage _cache;
    //private const string baseUrl = "http://localhost:";
    private readonly Uri _dockerUri = new Uri("unix:///var/run/docker.sock");
    public NodeRestoreService(ICacheStorage cacheStorage)
    {
        _cache = cacheStorage;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var dockerClient = new DockerClientConfiguration(_dockerUri).CreateClient())
        {
            dockerClient.DefaultTimeout = TimeSpan.FromMinutes(5);

            var containers = await dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool>
                        {
                            ["app=distributedCache"] = true
                        }
                    }
                });

            Dictionary<string, List<NodeDto>> nodes = new();
            List<NodeDto> masterNodes = new();
            foreach (var container in containers)
            {
                if (container.Labels["isReplica"] == "false")
                {
                    var name = container.Names[0].TrimStart('/');
                    NodeDto node = new()
                    {
                        Name = name,
                        //Url = baseUrl + container.Ports.FirstOrDefault()?.PublicPort,
                        Url = "http://" + name + ":" + 8080,
                        Id = container.ID
                    };

                    nodes[name] = new List<NodeDto>();
                    masterNodes.Add(node);
                }
            }
            foreach (var container in containers)
            {
                if (container.Labels["isReplica"] == "true")
                {
                    NodeDto node = new()
                    {
                        Name = container.Names[0].TrimStart('/'),
                        //Url = baseUrl + container.Ports.FirstOrDefault()?.PublicPort,
                        Url = "http://" + container.Names[0].TrimStart('/') + ":" + 8080,
                        Id = container.ID
                    };

                    if (nodes.TryGetValue(container.Labels["masterName"], out var value))
                    {
                        value.Add(node);
                    }
                }
            }
            foreach (var master in masterNodes)
            {
                if (nodes.TryGetValue(master.Name, out var value))
                {
                    RegisterNode(master, value);
                }
            }
        }
    }

    private bool RegisterNode(NodeDto masterNodeDto, List<NodeDto> replicas)
    {
        Node masterNode = new()
        {
            Name = masterNodeDto.Name,
            Status = NodeStatusEnum.Initializing,
            Id = masterNodeDto.Id,
            Url = new Uri(masterNodeDto.Url.EndsWith("/") ? masterNodeDto.Url : masterNodeDto.Url + "/")
        };

        for (int i = 0; i < replicas.Count; i++)
        {
            Node replica = new()
            {
                Name = replicas[i].Name,
                Status = NodeStatusEnum.Initializing,
                Id = replicas[i].Id,
                Url = new Uri(replicas[i].Url.EndsWith("/") ? replicas[i].Url : replicas[i].Url + "/")
            };

            masterNode.Replicas.Add(replica);
        }

        _cache.AddNode(masterNode.Name, masterNode);

        return true;
    }
}
