using ClusterManager.DTO;

namespace ClusterManager.Interfaces;

public interface INodeManager
{
    Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item);
    Task<Result<string?>> GetCacheItemAsync(string key);
    Task<Result<List<NodeWithDataResponseDto>>> GetAllNodesWithDataAsync();
    Task<Result<NodeWithDataResponseDto>> GetNodeWithDataAsync(string key);
    Task<Result<List<NodeStatusDto>>> GetAllNodeStatusesAsync();
    Task<Result<NodeStatusDto>> GetNodeStatusAsync(string key);
}
