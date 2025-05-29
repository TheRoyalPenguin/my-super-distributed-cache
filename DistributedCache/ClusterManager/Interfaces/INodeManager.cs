using ClusterManager.Common;
using ClusterManager.DTO;
using ClusterManager.Models;

namespace ClusterManager.Interfaces;

public interface INodeManager
{
    Task<Result<string?>> SetCacheItemAsync(CacheItemRequestDto item);
    Task<Result<string?>> GetCacheItemAsync(string key);
    Task<Result<List<NodeWithDataResponseDto>>> GetAllNodesWithDataAsync();
    Task<Result<string>> RebalanceAfterDeletingNode(Node node);
    Task<Result<string>> RebalanceAfterCreatingNode(Node creatingNode);
    Task<Result<string>> DeleteCacheItemAsync(string itemKey);
}
