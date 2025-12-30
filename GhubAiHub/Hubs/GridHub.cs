using Microsoft.AspNetCore.SignalR;
using GhubAiShared;
using GhubAiHub.Services;

namespace GhubAiHub.Hubs;

public class GridHub : Hub
{
    private readonly NodeRegistry _registry;
    private readonly ResponseManager _responses;

    public GridHub(NodeRegistry registry, ResponseManager responses)
    {
        _registry = registry;
        _responses = responses;
    }

    public async Task RegisterNode(NodeRegistration reg)
    {
        var meta = new NodeMetadata
        {
            ConnectionId = Context.ConnectionId,
            MachineName = reg.MachineName,
            LastHeartbeat = DateTime.UtcNow
        };
        meta.HostedModels.Clear();
        foreach (var m in reg.AvailableModels ?? Enumerable.Empty<string>()) meta.HostedModels.Add(m);

        _registry.AddOrUpdate(Context.ConnectionId, meta);

        await Clients.Caller.SendAsync("Registered");
    }

    public async Task StreamInferenceResponse(InferenceChunk chunk)
    {
        if (chunk == null) return;

        if (_responses.TryAddChunk(chunk.RequestId, chunk.TokenFragment, chunk.IsFinal))
        {
            // acknowledged
        }

        await Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
