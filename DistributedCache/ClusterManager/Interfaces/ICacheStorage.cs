using ClusterManager.DTO;

namespace ClusterManager.Interfaces;

public interface ICacheStorage
{
    IReadOnlyDictionary<string, Node> Nodes { get; }
    bool RemoveNodeByName(string name);
    bool AddNode(string name, Node node);
    string GetNodeKeyForItemKey(string key);
    Node GetNodeForName(string name);
}
