namespace Node.DTO;

public class CacheItemResponseDto
{
    public required string Key { get; init; }
    public required object Value { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastAccessed { get; init; }
    public TimeSpan? TTL { get; init; }
}
