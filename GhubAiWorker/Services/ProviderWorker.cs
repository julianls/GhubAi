using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GhubAiShared;
using System.Text.Json;

namespace GhubAiWorker.Services;

public class ProviderWorker : BackgroundService
{
    private readonly ILogger<ProviderWorker> _logger;
    private HubConnection? _connection;
    private readonly HttpClient _http = new();

    public ProviderWorker(ILogger<ProviderWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubUrl = Environment.GetEnvironmentVariable("HUB_URL") ?? "https://localhost:7197/gridhub";
        var providerToken = Environment.GetEnvironmentVariable("PROVIDER_TOKEN");
        var allowInvalidCerts = Environment.GetEnvironmentVariable("ALLOW_INVALID_CERTS") == "1";

        if (allowInvalidCerts)
        {
            _logger.LogWarning("ALLOW_INVALID_CERTS=1 detected � the client will accept untrusted TLS certificates. This is for development only.");
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(providerToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult(providerToken);
                }

                if (allowInvalidCerts)
                {
                    // Dev-only: accept invalid/untrusted server certificates. DO NOT USE IN PRODUCTION.
                    options.HttpMessageHandlerFactory = innerHandler =>
                    {
                        if (innerHandler is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        return innerHandler;
                    };
                }
            })
            .WithAutomaticReconnect()
            .Build();

        // Accept inference requests - now with simplified direct forwarding
        _connection.On<InferenceRequest>("RequestInference", async (req) => await HandleRequest(req));
        _connection.On<InferenceRequest>("RequestOpenAI", async (req) => await HandleRequest(req));

        // server invokes "Registered" without args
        _connection.On("Registered", () => _logger.LogInformation("Registered with hub"));

        _connection.Closed += async (ex) =>
        {
            _logger.LogWarning("Connection closed, reconnecting in 5s");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
                await StartConnection(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while reconnecting");
            }
        };

        await StartConnection(stoppingToken);

        // Periodic model discovery & registration
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tagsUrl = Environment.GetEnvironmentVariable("OLLAMA_LOCAL_URL") ?? "http://localhost:11434/api/tags";

                List<string> models = new();

                try
                {
                    // fetch raw JSON and attempt to parse flexible formats
                    var json = await _http.GetStringAsync(tagsUrl, stoppingToken);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            // array of strings
                            foreach (var el in root.EnumerateArray())
                            {
                                if (el.ValueKind == JsonValueKind.String)
                                {
                                    models.Add(el.GetString()!);
                                }
                                else if (el.ValueKind == JsonValueKind.Object)
                                {
                                    // object array: try properties
                                    if (el.TryGetProperty("model", out var pm) && pm.ValueKind == JsonValueKind.String)
                                        models.Add(pm.GetString()!);
                                    else if (el.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String)
                                        models.Add(pn.GetString()!);
                                }
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Object)
                        {
                            // object with 'models' array
                            if (root.TryGetProperty("models", out var modelsEl) && modelsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in modelsEl.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.String)
                                    {
                                        models.Add(item.GetString()!);
                                    }
                                    else if (item.ValueKind == JsonValueKind.Object)
                                    {
                                        if (item.TryGetProperty("model", out var pm) && pm.ValueKind == JsonValueKind.String)
                                            models.Add(pm.GetString()!);
                                        else if (item.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String)
                                            models.Add(pn.GetString()!);
                                        else if (item.TryGetProperty("remote_model", out var pr) && pr.ValueKind == JsonValueKind.String)
                                            models.Add(pr.GetString()!);
                                    }
                                }
                            }
                            else
                            {
                                // unexpected object shape - attempt to extract known props
                                if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                                    models.Add(name.GetString()!);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse tags response from {TagsUrl}", tagsUrl);
                }

                // Deduplicate and log
                models = models.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _logger.LogInformation("Discovered local models: {Models}", string.Join(",", models));

                var reg = new NodeRegistration(Environment.MachineName, models);
                if (_connection != null && _connection.State == HubConnectionState.Connected)
                {
                    await _connection.InvokeAsync("RegisterNode", reg, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task StartConnection(CancellationToken stoppingToken)
    {
        try
        {
            if (_connection == null) return;
            await _connection.StartAsync(stoppingToken);
            _logger.LogInformation("Connected to hub");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start connection");
        }
    }

    private async Task HandleRequest(InferenceRequest req)
    {
        try
        {
            // Build the full local Ollama URL using the base URL + the endpoint URI from the request
            var ollamaBase = Environment.GetEnvironmentVariable("OLLAMA_LOCAL_URL") ?? "http://localhost:11434";
            var localUrl = ollamaBase.TrimEnd('/') + "/" + req.EndpointUri.TrimStart('/');

            _logger.LogInformation("Forwarding request {RequestId} to {LocalUrl}", req.RequestId, localUrl);

            using var request = new HttpRequestMessage(HttpMethod.Post, localUrl)
            {
                Content = new StringContent(req.RequestBody, System.Text.Encoding.UTF8, "application/json")
            };

            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var eventLines = new List<string>();

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    // EOF: process any pending event
                    if (eventLines.Count > 0)
                        await ProcessEventLines(eventLines, req);
                    break;
                }

                // SSE event separator: blank line indicates end of event
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (eventLines.Count > 0)
                    {
                        await ProcessEventLines(eventLines, req);
                        eventLines.Clear();
                    }
                    continue;
                }

                eventLines.Add(line);
            }

            // final
            var finalChunk = new InferenceChunk(req.RequestId, string.Empty, true);
            if (_connection != null)
                await _connection.SendAsync("StreamInferenceResponse", finalChunk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request {RequestId}", req.RequestId);
            // send final with error indication
            if (_connection != null && req != null)
            {
                var finalChunk = new InferenceChunk(req.RequestId, $"[error] {ex.Message}", true);
                await _connection.SendAsync("StreamInferenceResponse", finalChunk);
            }
        }
    }

    private async Task ProcessEventLines(List<string> eventLines, InferenceRequest req)
    {
        // Combine data: lines
        var dataBuilder = new System.Text.StringBuilder();
        foreach (var l in eventLines)
        {
            var trimmed = l.StartsWith("data: ") ? l.Substring(6) : l;
            if (dataBuilder.Length > 0) dataBuilder.Append('\n');
            dataBuilder.Append(trimmed);
        }

        var combined = dataBuilder.ToString();
        if (string.IsNullOrWhiteSpace(combined)) return;

        // Handle common SSE termination token
        if (combined.Trim() == "[DONE]")
        {
            var finalChunk = new InferenceChunk(req.RequestId, string.Empty, true);
            if (_connection != null)
                await _connection.SendAsync("StreamInferenceResponse", finalChunk);
            return;
        }

        // Stream the raw response directly without transformation
        // The controller will handle any format conversion needed
        var chunk = new InferenceChunk(req.RequestId, combined, false);
        if (_connection != null)
            await _connection.SendAsync("StreamInferenceResponse", chunk);
    }
}
