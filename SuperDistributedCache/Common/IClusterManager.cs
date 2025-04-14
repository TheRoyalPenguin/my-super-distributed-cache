namespace Common;

public interface IClusterManager
{
    ICacheNode GetNodeForKey(string key);
    void RegisterNode(ICacheNode node);
    void UnregisterNode(ICacheNode node);
    T GetCacheItemForKey<T>(string key);
    void SetCacheItem<T>(ICacheItem item);
}
