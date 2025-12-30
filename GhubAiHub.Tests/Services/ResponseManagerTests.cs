using GhubAiHub.Services;
using Xunit;

namespace GhubAiHub.Tests.Services;

public class ResponseManagerTests
{
    [Fact]
    public void CreateChannelForRequest_ShouldCreateNewChannel()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";

        // Act
        var reader = manager.CreateChannelForRequest(requestId);

        // Assert
        Assert.NotNull(reader);
    }

    [Fact]
    public void TryAddChunk_ShouldReturnTrue_WhenChannelExists()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        manager.CreateChannelForRequest(requestId);

        // Act
        var result = manager.TryAddChunk(requestId, "chunk1", false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryAddChunk_ShouldReturnFalse_WhenChannelDoesNotExist()
    {
        // Arrange
        var manager = new ResponseManager();

        // Act
        var result = manager.TryAddChunk("nonexistent", "chunk1", false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TryAddChunk_ShouldWriteToChannel()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        var reader = manager.CreateChannelForRequest(requestId);

        // Act
        manager.TryAddChunk(requestId, "chunk1", false);
        manager.TryAddChunk(requestId, "chunk2", false);
        manager.TryAddChunk(requestId, "chunk3", true);

        // Assert
        var chunks = new List<string>();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            chunks.Add(chunk);
        }

        Assert.Equal(3, chunks.Count);
        Assert.Equal("chunk1", chunks[0]);
        Assert.Equal("chunk2", chunks[1]);
        Assert.Equal("chunk3", chunks[2]);
    }

    [Fact]
    public async Task TryAddChunk_WithIsFinalTrue_ShouldCompleteChannel()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        var reader = manager.CreateChannelForRequest(requestId);

        // Act
        manager.TryAddChunk(requestId, "final chunk", true);

        // Assert - Channel should be completed
        var chunks = new List<string>();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            chunks.Add(chunk);
        }

        Assert.Single(chunks);
        Assert.Equal("final chunk", chunks[0]);
        
        // Attempting to read again should complete immediately
        var completed = reader.Completion.IsCompleted;
        Assert.True(completed);
    }

    [Fact]
    public void Remove_ShouldRemoveChannel()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        manager.CreateChannelForRequest(requestId);

        // Act
        manager.Remove(requestId);

        // Assert
        var result = manager.TryAddChunk(requestId, "chunk", false);
        Assert.False(result);
    }

    [Fact]
    public void Remove_ShouldDoNothing_WhenChannelDoesNotExist()
    {
        // Arrange
        var manager = new ResponseManager();

        // Act & Assert (should not throw)
        manager.Remove("nonexistent");
    }

    [Fact]
    public async Task Remove_ShouldCompleteChannel()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        var reader = manager.CreateChannelForRequest(requestId);

        // Act
        manager.TryAddChunk(requestId, "chunk1", false);
        manager.Remove(requestId);

        // Assert - Reader should complete
        var chunks = new List<string>();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            chunks.Add(chunk);
        }

        Assert.Single(chunks);
        Assert.Equal("chunk1", chunks[0]);
    }

    [Fact]
    public void CreateChannelForRequest_ShouldAllowMultipleRequests()
    {
        // Arrange
        var manager = new ResponseManager();

        // Act
        var reader1 = manager.CreateChannelForRequest("req1");
        var reader2 = manager.CreateChannelForRequest("req2");
        var reader3 = manager.CreateChannelForRequest("req3");

        // Assert
        Assert.NotNull(reader1);
        Assert.NotNull(reader2);
        Assert.NotNull(reader3);

        manager.TryAddChunk("req1", "chunk1", false);
        manager.TryAddChunk("req2", "chunk2", false);
        manager.TryAddChunk("req3", "chunk3", false);

        Assert.True(manager.TryAddChunk("req1", "more", false));
        Assert.True(manager.TryAddChunk("req2", "more", false));
        Assert.True(manager.TryAddChunk("req3", "more", false));
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldHandleMultipleRequestsSimultaneously()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestIds = Enumerable.Range(1, 10).Select(i => $"req{i}").ToList();
        var readers = requestIds.Select(id => (id, reader: manager.CreateChannelForRequest(id))).ToList();

        // Act - Add chunks concurrently
        var writeTasks = requestIds.Select(async requestId =>
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    manager.TryAddChunk(requestId, $"chunk{i}", i == 4);
                }
            });
        });

        await Task.WhenAll(writeTasks);

        // Assert - All readers should receive their chunks
        foreach (var (id, reader) in readers)
        {
            var chunks = new List<string>();
            await foreach (var chunk in reader.ReadAllAsync())
            {
                chunks.Add(chunk);
            }

            Assert.Equal(5, chunks.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"chunk{i}", chunks[i]);
            }
        }
    }

    [Fact]
    public async Task TryAddChunk_WithEmptyChunk_ShouldStillWrite()
    {
        // Arrange
        var manager = new ResponseManager();
        var requestId = "req123";
        var reader = manager.CreateChannelForRequest(requestId);

        // Act
        manager.TryAddChunk(requestId, "", false);
        manager.TryAddChunk(requestId, "data", true);

        // Assert
        var chunks = new List<string>();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);
        Assert.Equal("", chunks[0]);
        Assert.Equal("data", chunks[1]);
    }
}
