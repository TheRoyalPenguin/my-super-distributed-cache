using Common;

namespace ClusterManager;

class ClusterManager : IClusterManager
{
    private readonly List<ICacheNode> _nodes = new();

    public void RegisterNode(ICacheNode node)
    {
        _nodes.Add(node);
    }

    public void UnregisterNode(ICacheNode node)
    {
        _nodes.Remove(node);
    }

    public ICacheNode GetNodeForKey(string itemKey)
    {
        int hash = itemKey.GetHashCode();
        int index = Math.Abs(hash) % _nodes.Count;
        return _nodes[index];
    }

    public T GetCacheItemForKey<T>(string itemKey)
    {
        var node = GetNodeForKey(itemKey);
        var item = node.Get<T>(itemKey);
        return item;
    }

    public void SetCacheItem<T>(ICacheItem cacheItem)
    {
        var node = GetNodeForKey(cacheItem.Key);
        node.Set<T>(cacheItem.Key, (T)cacheItem.Value, cacheItem.TTL);
    }
}
