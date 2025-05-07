namespace ClusterManager.DTO;

public class NodeWithDataResponseDto
{
    public Uri Url { get; set; }
    public string Id { get; set; }
    public List<CacheItemResponseDto> Items { get; init;  } = new();
}
