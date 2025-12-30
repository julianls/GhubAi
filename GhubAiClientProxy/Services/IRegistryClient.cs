using GhubAiClientProxy.Models;

namespace GhubAiClientProxy.Services
{
    public interface IRegistryClient
    {
        Task<IEnumerable<HubInstance>> GetHubInstancesAsync(CancellationToken cancellationToken = default);
    }
}
