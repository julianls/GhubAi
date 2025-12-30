using GhubAiClientProxy.Models;
using GhubAiClientProxy.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace GhubAiClientProxy.Tests.Services;

public class DynamicProxyConfigProviderTests
{
    private readonly Mock<IRegistryClient> _mockRegistryClient;
    private readonly Mock<ILogger<DynamicProxyConfigProvider>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public DynamicProxyConfigProviderTests()
    {
        _mockRegistryClient = new Mock<IRegistryClient>();
        _mockLogger = new Mock<ILogger<DynamicProxyConfigProvider>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    }

    [Fact]
    public void GetConfig_ShouldReturnInitialEmptyConfig()
    {
        // Arrange
        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        var config = provider.GetConfig();

        // Assert
        Assert.NotNull(config);
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldDoNothing_WhenNoHubsAvailable()
    {
        // Arrange
        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<HubInstance>());

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert
        var config = provider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldUpdateConfig_WithHealthyHubs()
    {
        // Arrange
        var hubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" },
            new HubInstance { Address = "http://localhost:5002" }
        };

        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hubs);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(httpMessageHandler.Object));

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert
        var config = provider.GetConfig();
        Assert.NotEmpty(config.Routes);
        Assert.NotEmpty(config.Clusters);
        Assert.Single(config.Routes);
        Assert.Single(config.Clusters);
        
        var route = config.Routes.First();
        Assert.Equal("ai-api-route", route.RouteId);
        Assert.Equal("hub-cluster", route.ClusterId);
        Assert.Equal("/v1/{**catch-all}", route.Match.Path);

        var cluster = config.Clusters.First();
        Assert.Equal("hub-cluster", cluster.ClusterId);
        Assert.Equal("RoundRobin", cluster.LoadBalancingPolicy);
        Assert.Equal(2, cluster.Destinations!.Count);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldExcludeUnhealthyHubs()
    {
        // Arrange
        var hubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" },
            new HubInstance { Address = "http://localhost:5002" }
        };

        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hubs);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        
        // First hub is healthy, second is not
        var callCount = 0;
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = callCount == 1 ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable
                };
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert
        var config = provider.GetConfig();
        var cluster = config.Clusters.First();
        Assert.Single(cluster.Destinations!); // Only one healthy hub
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldDoNothing_WhenNoHealthyHubs()
    {
        // Arrange
        var hubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" }
        };

        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hubs);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert
        var config = provider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldHandleExceptions()
    {
        // Arrange
        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Registry error"));

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act & Assert (should not throw)
        await provider.UpdateConfigAsync();

        var config = provider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldHandleHealthCheckTimeout()
    {
        // Arrange
        var hubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" }
        };

        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hubs);

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert - No healthy hubs should result in no config update
        var config = provider.GetConfig();
        Assert.Empty(config.Routes);
        Assert.Empty(config.Clusters);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldCheckHealthEndpoint()
    {
        // Arrange
        var hubAddress = "http://localhost:5001";
        var hubs = new List<HubInstance>
        {
            new HubInstance { Address = hubAddress }
        };

        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hubs);

        HttpRequestMessage? capturedRequest = null;
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal($"{hubAddress}/health", capturedRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldHandleMultipleUpdates()
    {
        // Arrange
        var hubs1 = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" }
        };

        var hubs2 = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" },
            new HubInstance { Address = "http://localhost:5002" }
        };

        var callCount = 0;
        _mockRegistryClient
            .Setup(x => x.GetHubInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? hubs1 : hubs2;
            });

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(httpMessageHandler.Object));

        var provider = new DynamicProxyConfigProvider(
            _mockRegistryClient.Object,
            _mockLogger.Object,
            _mockHttpClientFactory.Object);

        // Act
        await provider.UpdateConfigAsync();
        var config1 = provider.GetConfig();
        
        await provider.UpdateConfigAsync();
        var config2 = provider.GetConfig();

        // Assert
        Assert.Single(config1.Clusters.First().Destinations!);
        Assert.Equal(2, config2.Clusters.First().Destinations!.Count);
    }
}
