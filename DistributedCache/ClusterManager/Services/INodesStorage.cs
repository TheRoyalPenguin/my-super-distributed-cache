namespace ClusterManager.Services;

public interface INodesStorage
{
    List<Uri> Nodes { get; }
}
