namespace Node.DTO;

public class CacheItemDto
{
    public required string Key { get; init; }
    public required object Value { get; set; }
    public TimeSpan? TTL { get; init; }
}
