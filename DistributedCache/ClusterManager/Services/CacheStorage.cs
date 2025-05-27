using System.Collections.ObjectModel;
using ClusterManager.Common;
using ClusterManager.Common.Utils;
using ClusterManager.Enums;
using ClusterManager.Interfaces;
using ClusterManager.Models;

namespace ClusterManager.Services;

public class CacheStorage : ICacheStorage
{
    private readonly SortedList<string, Node> _nodes = new();
    private readonly List<string> _sortedKeys;
    private readonly object _lock = new();

    public IReadOnlyDictionary<string, Node> Nodes
        => new ReadOnlyDictionary<string, Node>(_nodes);

    public CacheStorage()
    {
        _sortedKeys = _nodes.Keys.ToList();
    }
    public bool SetNodeStatus(NodeStatusEnum newStatus, Node node)
    {
        lock(_lock)
        {
            node.Status = newStatus;
        }

        return true;
    }
    public bool RemoveMasterWithReplicas(string containerName)
    {
        var hashNode = HashGenerator.GetMd5HashString(containerName);
        lock (_lock)
        {
            _nodes.Remove(hashNode);
            UpdateSortedKeys(hashNode, false);
        }

        return true;
    }
    public bool AddNode(string name, Node node)
    {
        var hashNode = HashGenerator.GetMd5HashString(name);
        Console.WriteLine(name + ":" + hashNode);
        lock (_lock)
        {
            _nodes.Add(hashNode, node);
            UpdateSortedKeys(hashNode, true);
        }

        return true;
    }
    public string GetNodeKeyForItemKey(string key)
    {
        lock(_lock)
        {
            if (_nodes.Count == 0)
                throw new InvalidOperationException("Нет доступных нод для кэширования.");

            var hash = HashGenerator.GetMd5HashString(key);

            int idx = _sortedKeys.BinarySearch(hash, StringComparer.Ordinal);

            if (idx < 0)
            {
                idx = ~idx;
                if (idx >= _sortedKeys.Count)
                    idx = 0;
            }

            return _sortedKeys[idx];
        }
    }
    public Node GetNodeByName(string name)
    {
        var hashNode = HashGenerator.GetMd5HashString(name);
        lock(_lock)
        {
            var node = _nodes[hashNode]; 
            return node;
        }
    }
    public Result<Node> GetNextNode(string name)
    {
        var hashNode = HashGenerator.GetMd5HashString(name);

        var idx = _sortedKeys.BinarySearch(hashNode, StringComparer.Ordinal);
        if (idx >= 0)
        {
            idx++;
            if (idx >= _sortedKeys.Count)
                idx = 0;
        }
        else
        {
            return Result<Node>.Fail("Узла с переданным именем не существует.", 404);
        }
        lock (_lock)
        {
            var node = _nodes[_sortedKeys[idx]];
            return Result<Node>.Ok(node, 200);
        }
    }
    private void UpdateSortedKeys(string hashNode, bool isAdd)
    {
        var idx = _sortedKeys.BinarySearch(hashNode, StringComparer.Ordinal);
        if (idx < 0 && isAdd)
            _sortedKeys.Insert(~idx, hashNode);
        else if (idx >= 0 && !isAdd)
            _sortedKeys.RemoveAt(idx);
    }
}
