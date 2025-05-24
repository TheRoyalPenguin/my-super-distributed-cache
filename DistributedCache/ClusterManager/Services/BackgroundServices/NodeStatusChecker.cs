using ClusterManager.Common;
using ClusterManager.Enums;
using ClusterManager.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace ClusterManager.Services.BackgroundServices;

public class NodeStatusChecker(ICacheStorage _cacheStorage) : BackgroundService
{
    private readonly Uri _dockerUri = new Uri("npipe://./pipe/docker_engine");
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var containersResult = await GetContainersAsync();
            var containers = containersResult.Data;

            var nodes = _cacheStorage.Nodes.Values;
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

                node.Status = status;
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
