using GhubAiClientProxy.Models;
using GhubAiClientProxy.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace GhubAiClientProxy.Tests.Services;

public class RegistryClientTests
{
    private readonly Mock<ILogger<RegistryClient>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public RegistryClientTests()
    {
        _mockLogger = new Mock<ILogger<RegistryClient>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldReturnHubInstances_WhenRequestSucceeds()
    {
        // Arrange
        var expectedHubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" },
            new HubInstance { Address = "http://localhost:5002" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedHubs))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        var hubList = result.ToList();
        Assert.Equal(2, hubList.Count);
        Assert.Equal("http://localhost:5001", hubList[0].Address);
        Assert.Equal("http://localhost:5002", hubList[1].Address);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldReturnEmptyList_WhenRequestFails()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldReturnEmptyList_WhenExceptionThrown()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldUseDefaultUrl_WhenConfigNotProvided()
    {
        // Arrange
        var expectedHubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == "http://localhost:5120/Registry"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedHubs))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns((string?)null);

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldUseCustomUrl_WhenConfigProvided()
    {
        // Arrange
        var customUrl = "http://custom-registry:8080";
        var expectedHubs = new List<HubInstance>
        {
            new HubInstance { Address = "http://localhost:5001" }
        };

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == $"{customUrl}/Registry"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedHubs))
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns(customUrl);

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldReturnEmptyList_WhenResponseIsNull()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldHandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHubInstancesAsync_ShouldReturnEmptyList_WhenResponseIsEmptyArray()
    {
        // Arrange
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _mockConfiguration.Setup(c => c["RegistryUrl"]).Returns("http://localhost:5120");

        var client = new RegistryClient(httpClient, _mockConfiguration.Object, _mockLogger.Object);

        // Act
        var result = await client.GetHubInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
