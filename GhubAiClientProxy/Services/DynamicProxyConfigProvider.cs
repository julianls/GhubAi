using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace GhubAiClientProxy.Services
{
    public class DynamicProxyConfigProvider : IProxyConfigProvider
    {
        private volatile InMemoryProxyConfig _config;
        private readonly IRegistryClient _registryClient;
        private readonly ILogger<DynamicProxyConfigProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public DynamicProxyConfigProvider(
            IRegistryClient registryClient, 
            ILogger<DynamicProxyConfigProvider> logger,
            IHttpClientFactory httpClientFactory)
        {
            _registryClient = registryClient;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
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

                // Filter only healthy hubs
                var healthyHubs = new List<Models.HubInstance>();
                foreach (var hub in hubList)
                {
                    if (await IsHealthyAsync(hub.Address, cancellationToken))
                    {
                        healthyHubs.Add(hub);
                        _logger.LogDebug("Hub {Address} is healthy", hub.Address);
                    }
                    else
                    {
                        _logger.LogWarning("Hub {Address} failed health check, excluding from proxy", hub.Address);
                    }
                }

                if (!healthyHubs.Any())
                {
                    _logger.LogWarning("No healthy hub instances available");
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

                var destinations = healthyHubs.Select((hub, index) => new
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

                _logger.LogInformation("Proxy configuration updated with {Count} healthy hub instances", healthyHubs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update proxy configuration");
            }
        }

        private async Task<bool> IsHealthyAsync(string address, CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                var healthUrl = $"{address.TrimEnd('/')}/health";
                var response = await httpClient.GetAsync(healthUrl, cancellationToken);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check failed for {Address}", address);
                return false;
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
