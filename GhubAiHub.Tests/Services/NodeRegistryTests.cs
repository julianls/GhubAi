using GhubAiHub.Services;
using GhubAiShared;
using Xunit;

namespace GhubAiHub.Tests.Services;

public class NodeRegistryTests
{
    [Fact]
    public void GetAll_ShouldReturnEmptyCollection_WhenNoNodesRegistered()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        var nodes = registry.GetAll();

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void AddOrUpdate_ShouldAddNewNode()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            MachineName = "Machine1",
            CurrentLoad = 0
        };
        metadata.HostedModels.Add("model1");

        // Act
        registry.AddOrUpdate(connectionId, metadata);

        // Assert
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal("Machine1", nodes[0].MachineName);
        Assert.Contains("model1", nodes[0].HostedModels);
    }

    [Fact]
    public void AddOrUpdate_ShouldUpdateExistingNode()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata1 = new NodeMetadata
        {
            ConnectionId = connectionId,
            MachineName = "Machine1",
            CurrentLoad = 5
        };
        metadata1.HostedModels.Add("model1");

        var metadata2 = new NodeMetadata
        {
            ConnectionId = connectionId,
            MachineName = "Machine2",
            CurrentLoad = 10
        };
        metadata2.HostedModels.Add("model2");
        metadata2.HostedModels.Add("model3");

        // Act
        registry.AddOrUpdate(connectionId, metadata1);
        registry.AddOrUpdate(connectionId, metadata2);

        // Assert
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal("Machine2", nodes[0].MachineName);
        Assert.Equal(10, nodes[0].CurrentLoad);
        Assert.Equal(2, nodes[0].HostedModels.Count);
        Assert.Contains("model2", nodes[0].HostedModels);
        Assert.Contains("model3", nodes[0].HostedModels);
        Assert.DoesNotContain("model1", nodes[0].HostedModels);
    }

    [Fact]
    public void Remove_ShouldRemoveExistingNode()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata { ConnectionId = connectionId };
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        registry.Remove(connectionId);

        // Assert
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Remove_ShouldDoNothing_WhenNodeDoesNotExist()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert (should not throw)
        registry.Remove("nonexistent");
    }

    [Fact]
    public void TryGetNodeForModel_ShouldReturnFalse_WhenNoNodesHostModel()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act
        var result = registry.TryGetNodeForModel("model1", out var node);

        // Assert
        Assert.False(result);
        Assert.Null(node);
    }

    [Fact]
    public void TryGetNodeForModel_ShouldReturnNode_WhenModelIsHosted()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            MachineName = "Machine1"
        };
        metadata.HostedModels.Add("model1");
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        var result = registry.TryGetNodeForModel("model1", out var node);

        // Assert
        Assert.True(result);
        Assert.NotNull(node);
        Assert.Equal("Machine1", node.MachineName);
    }

    [Fact]
    public void TryGetNodeForModel_ShouldBeCaseInsensitive()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata { ConnectionId = connectionId };
        metadata.HostedModels.Add("Model1");
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        var result = registry.TryGetNodeForModel("model1", out var node);

        // Assert
        Assert.True(result);
        Assert.NotNull(node);
    }

    [Fact]
    public void TryGetNodeForModel_ShouldReturnNodeWithLowestLoad()
    {
        // Arrange
        var registry = new NodeRegistry();
        
        var metadata1 = new NodeMetadata
        {
            ConnectionId = "conn1",
            MachineName = "Machine1",
            CurrentLoad = 10
        };
        metadata1.HostedModels.Add("model1");
        
        var metadata2 = new NodeMetadata
        {
            ConnectionId = "conn2",
            MachineName = "Machine2",
            CurrentLoad = 5
        };
        metadata2.HostedModels.Add("model1");

        var metadata3 = new NodeMetadata
        {
            ConnectionId = "conn3",
            MachineName = "Machine3",
            CurrentLoad = 15
        };
        metadata3.HostedModels.Add("model1");

        registry.AddOrUpdate("conn1", metadata1);
        registry.AddOrUpdate("conn2", metadata2);
        registry.AddOrUpdate("conn3", metadata3);

        // Act
        var result = registry.TryGetNodeForModel("model1", out var node);

        // Assert
        Assert.True(result);
        Assert.NotNull(node);
        Assert.Equal("Machine2", node.MachineName);
        Assert.Equal(5, node.CurrentLoad);
    }

    [Fact]
    public void IncrementLoad_ShouldIncreaseNodeLoad()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            CurrentLoad = 5
        };
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        registry.IncrementLoad(connectionId);

        // Assert
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal(6, nodes[0].CurrentLoad);
    }

    [Fact]
    public void IncrementLoad_ShouldDoNothing_WhenNodeDoesNotExist()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert (should not throw)
        registry.IncrementLoad("nonexistent");
    }

    [Fact]
    public void DecrementLoad_ShouldDecreaseNodeLoad()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            CurrentLoad = 5
        };
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        registry.DecrementLoad(connectionId);

        // Assert
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal(4, nodes[0].CurrentLoad);
    }

    [Fact]
    public void DecrementLoad_ShouldNotGoBelowZero()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            CurrentLoad = 0
        };
        registry.AddOrUpdate(connectionId, metadata);

        // Act
        registry.DecrementLoad(connectionId);

        // Assert
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal(0, nodes[0].CurrentLoad);
    }

    [Fact]
    public void DecrementLoad_ShouldDoNothing_WhenNodeDoesNotExist()
    {
        // Arrange
        var registry = new NodeRegistry();

        // Act & Assert (should not throw)
        registry.DecrementLoad("nonexistent");
    }

    [Fact]
    public async Task ThreadSafety_ShouldHandleConcurrentOperations()
    {
        // Arrange
        var registry = new NodeRegistry();
        var connectionId = "conn123";
        var metadata = new NodeMetadata
        {
            ConnectionId = connectionId,
            CurrentLoad = 0
        };
        registry.AddOrUpdate(connectionId, metadata);

        // Act - Concurrent increment operations
        var incrementTasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            incrementTasks.Add(Task.Run(() => registry.IncrementLoad(connectionId)));
        }

        await Task.WhenAll(incrementTasks);

        // Assert - Should have incremented 100 times (thread-safe operations)
        var nodes = registry.GetAll().ToList();
        Assert.Single(nodes);
        Assert.Equal(100, nodes[0].CurrentLoad);
        
        // Now test decrement
        var decrementTasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            decrementTasks.Add(Task.Run(() => registry.DecrementLoad(connectionId)));
        }

        await Task.WhenAll(decrementTasks);

        // Should return to 0
        Assert.Equal(0, nodes[0].CurrentLoad);
    }
}
