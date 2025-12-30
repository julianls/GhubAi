using GhubAiHub.Helpers;
using GhubAiHub.Hubs;
using GhubAiHub.Services;
using GhubAiShared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Threading.Channels;

namespace GhubAiHub.Controllers;

[ApiController]
[Route("v1")]
public class AIController : ControllerBase
{
    private readonly NodeRegistry _registry;
    private readonly ResponseManager _responses;
    private readonly IHubContext<GridHub> _hubContext;

    public AIController(NodeRegistry registry, ResponseManager responses, IHubContext<GridHub> hubContext)
    {
        _registry = registry;
        _responses = responses;
        _hubContext = hubContext;
    }

    // GET /v1/models - return list of available models across the pool
    [HttpGet("models")]
    public IActionResult GetModels()
    {
        var models = _registry.GetAll().SelectMany(n => n.HostedModels).Distinct(StringComparer.OrdinalIgnoreCase);

        var modelData = models.Select(m => new
        {
            id = m,
            @object = "model",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            owned_by = "library"
        }).ToList();

        var response = new
        {
            @object = "list",
            data = modelData
        };

        return Ok(response);
    }

    // POST /v1/chat/completions - OpenAI-style completion
    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] object payload)
    {
        var payloadStr = payload?.ToString() ?? string.Empty;
        var request = ParseChatRequest(payloadStr);

        if (string.IsNullOrEmpty(request.Model))
        {
            return BadRequest(new { error = "model missing" });
        }

        if (!_registry.TryGetNodeForModel(request.Model, out var node))
        {
            return NotFound(new { error = "no node with model" });
        }

        var requestId = Guid.NewGuid().ToString("n");
        var reader = _responses.CreateChannelForRequest(requestId);
        var inf = new InferenceRequest(requestId, request.Model, "/v1/chat/completions", payloadStr);

        _registry.IncrementLoad(node.ConnectionId);

        try
        {
            await _hubContext.Clients.Client(node.ConnectionId).SendAsync("RequestOpenAI", inf);

            if (request.Stream)
            {
                return new StreamSseResult(reader, request.IncludeUsage, request.Model, request.Prompt, requestId, async () =>
                {
                    _responses.Remove(requestId);
                    _registry.DecrementLoad(node.ConnectionId);
                });
            }

            return await HandleNonStreamingResponse(reader, request.Model, request.Prompt, requestId);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            if (!request.Stream)
            {
                _responses.Remove(requestId);
                _registry.DecrementLoad(node.ConnectionId);
            }
        }
    }

    private ChatRequestInfo ParseChatRequest(string payload)
    {
        string model = string.Empty;
        bool stream = false;
        bool includeUsage = false;
        string prompt = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("model", out var m))
                model = m.GetString() ?? string.Empty;

            if (root.TryGetProperty("stream", out var streamEl) && streamEl.ValueKind == JsonValueKind.True)
            {
                stream = true;
            }

            if (root.TryGetProperty("stream_options", out var so) && so.ValueKind == JsonValueKind.Object)
            {
                if (so.TryGetProperty("include_usage", out var iu) && iu.ValueKind == JsonValueKind.True)
                {
                    includeUsage = true;
                }
            }

            if (root.TryGetProperty("prompt", out var p))
                prompt = p.GetString() ?? string.Empty;

            if (string.IsNullOrEmpty(prompt) && root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.TryGetProperty("content", out var c))
                        prompt += (c.GetString() ?? string.Empty);
                }
            }
        }
        catch { }

        return new ChatRequestInfo(model, stream, includeUsage, prompt);
    }

    private async Task<IActionResult> HandleNonStreamingResponse(ChannelReader<string> reader, string model, string prompt, string requestId)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in reader.ReadAllAsync())
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                sb.Append(chunk);
            }
        }

        var resp = sb.ToString();
        return Ok(JsonDocument.Parse(resp));
    }

    private record ChatRequestInfo(string Model, bool Stream, bool IncludeUsage, string Prompt);
}
