namespace Node.DTO;

public class CacheItemDto
{
    public object Value { get; set; }
    public TimeSpan? TTL { get; set; }
}
