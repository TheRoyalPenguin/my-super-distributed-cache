using ClusterManager.DTO;

namespace ClusterManager.Interfaces;

public interface INodesService
{
    Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item);
    Task<Result<List<NodeDto>>> CreateNodeAsync(string containerName, int copiesCount);
    Task<Result<string?>> GetCacheItemAsync(string key);
    Task<Result<List<List<NodeWithDataResponseDto>>>> GetAllNodesWithDataAsync();
    Task<Result<NodeWithDataResponseDto>> GetNodeWithDataAsync(string key);
    Task<Result<List<NodeStatusDto>>> GetAllNodeStatusesAsync();
    Task<Result<NodeStatusDto>> GetNodeStatusAsync(string key);
}
