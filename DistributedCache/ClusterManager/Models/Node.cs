using ClusterManager.Enums;

namespace ClusterManager.Models;

public class Node
{
    public required string Name { get; init; }
    public required NodeStatusEnum Status { get; set; }
    public required Uri Url { get; init; }
    public required string Id { get; init; }
    public List<Node> Replicas { get; init; } = new();
}
