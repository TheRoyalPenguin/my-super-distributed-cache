using ClusterManager.DTO;

namespace ClusterManager.Interfaces;

public interface ICacheStorage
{
    IReadOnlyDictionary<string, Node> Nodes { get; }
    bool RemoveMasterWithReplicas(string name);
    bool AddNode(string name, Node node);
    string GetNodeKeyForItemKey(string key);
    Node GetNodeByName(string name);
    Result<Node> GetNextNode(string name);
}
