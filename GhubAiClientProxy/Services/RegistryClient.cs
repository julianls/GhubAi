using GhubAiClientProxy.Models;

namespace GhubAiClientProxy.Services
{
    public class RegistryClient : IRegistryClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RegistryClient> _logger;
        private readonly string _registryUrl;

        public RegistryClient(HttpClient httpClient, IConfiguration configuration, ILogger<RegistryClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _registryUrl = configuration["RegistryUrl"] ?? "http://localhost:5120";
        }

        public async Task<IEnumerable<HubInstance>> GetHubInstancesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_registryUrl}/Registry", cancellationToken);
                response.EnsureSuccessStatusCode();

                var hubs = await response.Content.ReadFromJsonAsync<IEnumerable<HubInstance>>(cancellationToken);
                return hubs ?? Enumerable.Empty<HubInstance>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch hub instances from registry at {RegistryUrl}", _registryUrl);
                return Enumerable.Empty<HubInstance>();
            }
        }
    }
}
