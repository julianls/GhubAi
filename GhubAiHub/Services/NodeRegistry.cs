using System.Collections.Concurrent;
using System.Threading;
using GhubAiShared;

namespace GhubAiHub.Services;

public class NodeRegistry
{
    private readonly ConcurrentDictionary<string, NodeMetadata> _nodes = new();

    public IEnumerable<NodeMetadata> GetAll() => _nodes.Values;

    public void AddOrUpdate(string connectionId, NodeMetadata meta)
    {
        _nodes.AddOrUpdate(connectionId, meta, (k, v) =>
        {
            v.MachineName = meta.MachineName;
            v.LastHeartbeat = meta.LastHeartbeat;
            v.CurrentLoad = meta.CurrentLoad;
            v.HostedModels.Clear();
            foreach (var m in meta.HostedModels) v.HostedModels.Add(m);
            return v;
        });
    }

    public void Remove(string connectionId) => _nodes.TryRemove(connectionId, out _);

    public bool TryGetNodeForModel(string model, out NodeMetadata? node)
    {
        node = _nodes.Values.Where(n => n.HostedModels.Contains(model, StringComparer.OrdinalIgnoreCase))
            .OrderBy(n => n.CurrentLoad)
            .FirstOrDefault();
        return node != null;
    }

    public void IncrementLoad(string connectionId)
    {
        if (_nodes.TryGetValue(connectionId, out var node))
        {
            Interlocked.Increment(ref node.CurrentLoad);
        }
    }

    public void DecrementLoad(string connectionId)
    {
        if (_nodes.TryGetValue(connectionId, out var node))
        {
            Interlocked.Decrement(ref node.CurrentLoad);
            if (node.CurrentLoad < 0) node.CurrentLoad = 0;
        }
    }
}
