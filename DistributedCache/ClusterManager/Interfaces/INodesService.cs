using ClusterManager.DTO;
using Node.DTO;

namespace ClusterManager.Interfaces;

public interface INodesService
{
    Task<Result<string?>> SetCacheItemAsync(CacheItemDto item);
    Task<Result<NodeResponseDto>> CreateNodeAsync(string containerName);
    Task<Result<string?>> GetCacheItemAsync(string key);
}
