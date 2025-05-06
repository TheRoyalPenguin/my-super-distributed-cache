using ClusterManager.Interfaces;

namespace ClusterManager.Services;

public class NodesStorage : INodesStorage
{
    public List<Uri> Nodes { get; } = new();
}
