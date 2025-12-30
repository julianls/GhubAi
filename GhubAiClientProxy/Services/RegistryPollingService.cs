namespace GhubAiClientProxy.Services
{
    public class RegistryPollingService : BackgroundService
    {
        private readonly DynamicProxyConfigProvider _configProvider;
        private readonly ILogger<RegistryPollingService> _logger;
        private readonly TimeSpan _pollingInterval;

        public RegistryPollingService(
            DynamicProxyConfigProvider configProvider,
            IConfiguration configuration,
            ILogger<RegistryPollingService> logger)
        {
            _configProvider = configProvider;
            _logger = logger;
            
            var intervalSeconds = configuration.GetValue<int>("RegistryPollingIntervalSeconds", 30);
            _pollingInterval = TimeSpan.FromSeconds(intervalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Registry polling service starting with interval {Interval}", _pollingInterval);

            // Initial load
            await _configProvider.UpdateConfigAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                    await _configProvider.UpdateConfigAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in registry polling service");
                }
            }

            _logger.LogInformation("Registry polling service stopped");
        }
    }
}
