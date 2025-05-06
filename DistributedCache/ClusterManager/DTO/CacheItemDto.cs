namespace Node.DTO;

public class CacheItemDto
{
    public string Key { get; init; }
    public object Value { get; set; }
    public TimeSpan? TTL { get; init; }
}
