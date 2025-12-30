using Microsoft.AspNetCore.Mvc;
using System.Threading.Channels;

namespace GhubAiHub.Helpers;

internal class StreamSseResult : IActionResult
{
    private readonly ChannelReader<string> _reader;
    private readonly bool _includeUsage;
    private readonly string _model;
    private readonly string _prompt;
    private readonly string _requestId;
    private readonly Func<Task> _onCompleted;

    public StreamSseResult(ChannelReader<string> reader, bool includeUsage, string model, string prompt, string requestId, Func<Task> onCompleted)
    {
        _reader = reader;
        _includeUsage = includeUsage;
        _model = model;
        _prompt = prompt;
        _requestId = requestId;
        _onCompleted = onCompleted;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        response.ContentType = "text/event-stream";
        response.StatusCode = 200;

        var assistantBuilder = new System.Text.StringBuilder();

        try
        {
            await foreach (var chunk in _reader.ReadAllAsync())
            {
                if (string.IsNullOrEmpty(chunk)) continue;

                await ProcessAndWriteChunks(response, chunk, assistantBuilder);
            }
            await response.WriteAsync("data: [DONE]\n\n");
            await response.Body.FlushAsync();
        }
        finally
        {
            if (_onCompleted is not null)
            {
                try { await _onCompleted(); } catch { }
            }
        }
    }

    private async Task ProcessAndWriteChunks(HttpResponse response, string chunk, System.Text.StringBuilder assistantBuilder)
    {
        var lines = chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var raw = line.StartsWith("data:") ? line.Substring(5).Trim() : line;
            await response.WriteAsync($"data: {raw}\n\n");
            await response.Body.FlushAsync();
        }
    }
}