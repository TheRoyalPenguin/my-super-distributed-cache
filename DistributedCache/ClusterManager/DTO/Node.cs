namespace ClusterManager.DTO;

public class Node
{
    public string Name { get; set; }
    public Uri Url { get; set; }
    public string Id { get; set; }
    public List<Node> Replicas { get; init; } = new();
}
