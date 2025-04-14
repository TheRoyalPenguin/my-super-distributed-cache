using Common;

namespace Client;

class DistributedCacheClient
{
    private readonly IClusterManager _clusterManager;
    public DistributedCacheClient(IClusterManager clusterManager)
    {
        _clusterManager = clusterManager;
    }

    public T Get<T>(string key)
    {
        return _clusterManager.GetCacheItemForKey<T>(key);
    }

    public void Set<T>(ICacheItem item)
    {
        _clusterManager.SetCacheItem<T>(item);
    }
}
