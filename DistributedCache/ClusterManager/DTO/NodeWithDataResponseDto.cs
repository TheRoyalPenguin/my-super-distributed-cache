namespace ClusterManager.DTO;

public class NodeWithDataResponseDto
{
    public string Name { get; set; }
    public Uri Url { get; set; }
    public string Id { get; set; }
    public List<CacheItemResponseDto> Items { get; init;  } = new();
    public List<NodeWithDataResponseDto> Replicas { get; init; } = new();
}
