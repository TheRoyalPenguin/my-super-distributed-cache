namespace ClusterManager.DTO;

public class CacheItemRequestDto
{
    public string Key { get; init; }
    public object Value { get; set; }
    public TimeSpan? TTL { get; init; }
}
