using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GhubAiHub.Services;

public class ResponseManager
{
    private readonly ConcurrentDictionary<string, Channel<string>> _channels = new();

    public ChannelReader<string> CreateChannelForRequest(string requestId)
    {
        var channel = Channel.CreateUnbounded<string>();
        _channels[requestId] = channel;
        return channel.Reader;
    }

    public bool TryAddChunk(string requestId, string chunk, bool isFinal)
    {
        if (_channels.TryGetValue(requestId, out var channel))
        {
            channel.Writer.TryWrite(chunk);
            if (isFinal) channel.Writer.TryComplete();
            return true;
        }
        return false;
    }

    public void Remove(string requestId)
    {
        if (_channels.TryRemove(requestId, out var ch))
        {
            ch.Writer.TryComplete();
        }
    }
}
