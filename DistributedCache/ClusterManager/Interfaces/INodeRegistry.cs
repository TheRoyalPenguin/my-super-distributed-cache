using ClusterManager.DTO;

namespace ClusterManager.Interfaces;

public interface INodeRegistry
{
    Task<Result<List<NodeDto>>> CreateNodeAsync(string containerName, int copiesCount);
    Task<Result<List<string>>> DeleteNodeByNameAsync(string name);
    Task<Result<List<string>>> ForceDeleteNodeByNameAsync(string containerName);
}
