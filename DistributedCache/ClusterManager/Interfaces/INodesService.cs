using ClusterManager.DTO;
using Node.DTO;

namespace ClusterManager.Interfaces;

public interface INodesService
{
    Task<Result<string?>> SetCacheItemAsync(CacheItemDto item);
    Task<Result<List<NodeResponseDto>>> CreateNodeAsync(string containerName, int copiesCount);
    Task<Result<string?>> GetCacheItemAsync(string key);
}
