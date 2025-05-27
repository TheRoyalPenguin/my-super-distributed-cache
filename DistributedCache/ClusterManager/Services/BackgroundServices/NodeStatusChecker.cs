using ClusterManager.Common;
using ClusterManager.Enums;
using ClusterManager.Interfaces;
using ClusterManager.Models;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace ClusterManager.Services.BackgroundServices;

public class NodeStatusChecker(ICacheStorage _cacheStorage, IServiceScopeFactory _scopeFactory) : BackgroundService
{
    private readonly Uri _dockerUri = new Uri("unix:///var/run/docker.sock");
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<INodeManager>();

            var containersResult = await GetContainersAsync();
            var containers = containersResult.Data;

            var masterNodes = _cacheStorage.Nodes.Values.ToList();

            foreach(var mn in masterNodes)
            {
                var nodeContainerId = mn.Id;
                var container = containers.FirstOrDefault(c => c.ID == nodeContainerId);
                var status = container != null ? container.State switch
                {
                    "running" => NodeStatusEnum.Online,
                    "created" => NodeStatusEnum.Initializing,
                    "restarting" => NodeStatusEnum.Initializing,
                    "paused" => NodeStatusEnum.Offline,
                    "exited" => NodeStatusEnum.Offline,
                    "dead" => NodeStatusEnum.Error,
                    _ => NodeStatusEnum.Error,
                } : NodeStatusEnum.ContainerNotFound;

                if (mn.Status != NodeStatusEnum.Online && status == NodeStatusEnum.Online)
                {
                    await manager.RebalanceAfterCreatingNode(mn);
                }
            }

            var nodes = masterNodes
                .SelectMany(master => new[] { master }.Concat(master.Replicas ?? new List<Node>()))
                .ToList();

            foreach (var node in nodes)
            {
                var nodeContainerId = node.Id;
                var container = containers.FirstOrDefault(c => c.ID == nodeContainerId);
                var status = container != null ? container.State switch
                {
                    "running" => NodeStatusEnum.Online,
                    "created" => NodeStatusEnum.Initializing,
                    "restarting" => NodeStatusEnum.Initializing,
                    "paused" => NodeStatusEnum.Offline,
                    "exited" => NodeStatusEnum.Offline,
                    "dead" => NodeStatusEnum.Error,
                    _ => NodeStatusEnum.Error,
                } : NodeStatusEnum.ContainerNotFound;

                _cacheStorage.SetNodeStatus(status, node);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
    private async Task<Result<IList<ContainerListResponse>>> GetContainersAsync()
    {
        using (var dockerClient = new DockerClientConfiguration(_dockerUri).CreateClient())
        {
            var containers = await dockerClient.Containers.ListContainersAsync(new ContainersListParameters
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

            return Result<IList<ContainerListResponse>>.Ok(containers, 200);
        }
    }
}
