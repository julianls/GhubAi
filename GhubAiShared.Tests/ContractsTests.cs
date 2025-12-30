using GhubAiShared;
using Xunit;

namespace GhubAiShared.Tests;

public class NodeRegistrationTests
{
    [Fact]
    public void NodeRegistration_ShouldInitializeWithMachineNameAndModels()
    {
        // Arrange
        var machineName = "TestMachine";
        var models = new[] { "model1", "model2" };

        // Act
        var registration = new NodeRegistration(machineName, models);

        // Assert
        Assert.Equal(machineName, registration.MachineName);
        Assert.Equal(models, registration.AvailableModels);
    }

    [Fact]
    public void NodeRegistration_ShouldHandleEmptyModelsList()
    {
        // Arrange
        var machineName = "TestMachine";
        var models = Enumerable.Empty<string>();

        // Act
        var registration = new NodeRegistration(machineName, models);

        // Assert
        Assert.Equal(machineName, registration.MachineName);
        Assert.Empty(registration.AvailableModels);
    }
}

public class InferenceRequestTests
{
    [Fact]
    public void InferenceRequest_ShouldInitializeWithAllProperties()
    {
        // Arrange
        var requestId = "req123";
        var model = "testModel";
        var endpointUri = "/api/inference";
        var requestBody = "{\"prompt\":\"test\"}";

        // Act
        var request = new InferenceRequest(requestId, model, endpointUri, requestBody);

        // Assert
        Assert.Equal(requestId, request.RequestId);
        Assert.Equal(model, request.Model);
        Assert.Equal(endpointUri, request.EndpointUri);
        Assert.Equal(requestBody, request.RequestBody);
    }
}

public class InferenceChunkTests
{
    [Fact]
    public void InferenceChunk_ShouldInitializeWithDefaultIsFinal()
    {
        // Arrange
        var requestId = "req123";
        var tokenFragment = "test fragment";

        // Act
        var chunk = new InferenceChunk(requestId, tokenFragment);

        // Assert
        Assert.Equal(requestId, chunk.RequestId);
        Assert.Equal(tokenFragment, chunk.TokenFragment);
        Assert.False(chunk.IsFinal);
    }

    [Fact]
    public void InferenceChunk_ShouldSetIsFinalExplicitly()
    {
        // Arrange
        var requestId = "req123";
        var tokenFragment = "final fragment";

        // Act
        var chunk = new InferenceChunk(requestId, tokenFragment, true);

        // Assert
        Assert.Equal(requestId, chunk.RequestId);
        Assert.Equal(tokenFragment, chunk.TokenFragment);
        Assert.True(chunk.IsFinal);
    }
}

public class NodeMetadataTests
{
    [Fact]
    public void NodeMetadata_ShouldInitializeWithDefaultValues()
    {
        // Act
        var metadata = new NodeMetadata();

        // Assert
        Assert.Equal(string.Empty, metadata.ConnectionId);
        Assert.Equal(string.Empty, metadata.MachineName);
        Assert.Empty(metadata.HostedModels);
        Assert.Equal(0, metadata.CurrentLoad);
        Assert.True((DateTime.UtcNow - metadata.LastHeartbeat).TotalSeconds < 1);
    }

    [Fact]
    public void NodeMetadata_ShouldAllowModifyingProperties()
    {
        // Arrange
        var metadata = new NodeMetadata();
        var connectionId = "conn123";
        var machineName = "Machine1";
        var lastHeartbeat = DateTime.UtcNow.AddMinutes(-5);

        // Act
        metadata.ConnectionId = connectionId;
        metadata.MachineName = machineName;
        metadata.LastHeartbeat = lastHeartbeat;
        metadata.HostedModels.Add("model1");
        metadata.HostedModels.Add("model2");
        metadata.CurrentLoad = 5;

        // Assert
        Assert.Equal(connectionId, metadata.ConnectionId);
        Assert.Equal(machineName, metadata.MachineName);
        Assert.Equal(lastHeartbeat, metadata.LastHeartbeat);
        Assert.Equal(2, metadata.HostedModels.Count);
        Assert.Contains("model1", metadata.HostedModels);
        Assert.Contains("model2", metadata.HostedModels);
        Assert.Equal(5, metadata.CurrentLoad);
    }

    [Fact]
    public void NodeMetadata_HostedModels_ShouldBeCaseInsensitive()
    {
        // Arrange
        var metadata = new NodeMetadata();

        // Act
        metadata.HostedModels.Add("Model1");
        
        // Assert
        Assert.Contains("model1", metadata.HostedModels);
        Assert.Contains("MODEL1", metadata.HostedModels);
        Assert.Contains("Model1", metadata.HostedModels);
    }
}
