using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace GhubAiClientProxy.Services
{
    public class DynamicProxyConfigProvider : IProxyConfigProvider
    {
        private volatile InMemoryProxyConfig _config;
        private readonly IRegistryClient _registryClient;
        private readonly ILogger<DynamicProxyConfigProvider> _logger;

        public DynamicProxyConfigProvider(IRegistryClient registryClient, ILogger<DynamicProxyConfigProvider> logger)
        {
            _registryClient = registryClient;
            _logger = logger;
            _config = new InMemoryProxyConfig(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());
        }

        public IProxyConfig GetConfig() => _config;

        public async Task UpdateConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var hubs = await _registryClient.GetHubInstancesAsync(cancellationToken);
                var hubList = hubs.ToList();

                if (!hubList.Any())
                {
                    _logger.LogWarning("No hub instances available from registry");
                    return;
                }

                // Only route OpenAI-compatible API endpoints (/v1/*)
                var routes = new[]
                {
                    new RouteConfig
                    {
                        RouteId = "ai-api-route",
                        ClusterId = "hub-cluster",
                        Match = new RouteMatch
                        {
                            Path = "/v1/{**catch-all}"
                        }
                    }
                };

                var destinations = hubList.Select((hub, index) => new
                {
                    Key = $"destination{index}",
                    Value = new DestinationConfig
                    {
                        Address = hub.Address
                    }
                }).ToDictionary(x => x.Key, x => x.Value);

                var clusters = new[]
                {
                    new ClusterConfig
                    {
                        ClusterId = "hub-cluster",
                        Destinations = destinations,
                        LoadBalancingPolicy = "RoundRobin"
                    }
                };

                var oldConfig = _config;
                _config = new InMemoryProxyConfig(routes, clusters);
                oldConfig.SignalChange();

                _logger.LogInformation("Proxy configuration updated with {Count} hub instances", hubList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update proxy configuration");
            }
        }

        private class InMemoryProxyConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new();

            public InMemoryProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<RouteConfig> Routes { get; }
            public IReadOnlyList<ClusterConfig> Clusters { get; }
            public IChangeToken ChangeToken { get; }

            public void SignalChange()
            {
                _cts.Cancel();
            }
        }
    }
}
