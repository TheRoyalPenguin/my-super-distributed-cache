namespace ClusterManager.Models;

public class Node
{
    public string Name { get; init; }
    public Uri Url { get; init; }
    public string Id { get; init; }
    public List<Node> Replicas { get; init; } = new();
}
