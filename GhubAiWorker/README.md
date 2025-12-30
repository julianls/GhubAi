# GhubAiWorker

A worker node that connects to GhubAiHub via SignalR, discovers local Ollama models, and processes AI inference requests by forwarding them to a local Ollama instance.

## Overview

GhubAiWorker is a background service that acts as a bridge between the GhubAi distributed system and a local Ollama installation. It automatically discovers available models, registers with the hub, and processes inference requests with real-time streaming responses.

## Features

- **Automatic Model Discovery**: Polls local Ollama instance to discover available models
- **SignalR Communication**: Real-time bi-directional communication with the hub
- **Streaming Inference**: Forwards requests to Ollama and streams responses back via SSE
- **Auto-Reconnection**: Automatically reconnects to hub on connection loss
- **Flexible Authentication**: Supports token-based authentication
- **Development Mode**: Optional certificate validation bypass for local development
- **Format Flexibility**: Handles various Ollama API response formats
- **Aspire Integration**: Built-in OpenTelemetry and health checks

## Architecture

```
GhubAiHub <--[SignalR]--> GhubAiWorker <--[HTTP]--> Local Ollama
                               |
                       [Model Discovery]
                               |
                         Ollama /api/tags
```

## Components

### Services

- **ProviderWorker**: Background service (`BackgroundService`) that manages hub connection and request processing

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `HUB_URL` | SignalR hub URL | `https://localhost:7197/gridhub` |
| `PROVIDER_TOKEN` | Authentication token for hub | (optional) |
| `OLLAMA_LOCAL_URL` | Local Ollama base URL | `http://localhost:11434` |
| `ALLOW_INVALID_CERTS` | Accept untrusted TLS certificates (dev only) | `0` |

### Example Configuration

```bash
export HUB_URL=https://hub.example.com/gridhub
export PROVIDER_TOKEN=your-secure-token
export OLLAMA_LOCAL_URL=http://localhost:11434
export ALLOW_INVALID_CERTS=0
```

## Running the Service

### Prerequisites

- .NET 10.0 SDK
- Ollama installed and running locally
- Access to a running GhubAiHub instance

### Local Development

```bash
dotnet run
```

### Docker

```bash
docker build -t ghubai-worker .
docker run \
  -e HUB_URL=https://hub:7197/gridhub \
  -e PROVIDER_TOKEN=your-token \
  -e OLLAMA_LOCAL_URL=http://host.docker.internal:11434 \
  ghubai-worker
```

### With Ollama Models

Ensure Ollama is running with models pulled:

```bash
ollama pull llama2
ollama pull mistral
ollama serve
```

## How It Works

### 1. Startup & Connection

- Worker starts and builds SignalR connection to hub
- Registers handlers for `RequestInference` and `RequestOpenAI` messages
- Authenticates using `PROVIDER_TOKEN` if provided

### 2. Model Discovery

Every 30 seconds:
- Queries `{OLLAMA_LOCAL_URL}/api/tags`
- Parses response (supports multiple JSON formats)
- Sends `RegisterNode` with machine name and model list

### 3. Request Processing

When hub sends inference request:
- Constructs full Ollama endpoint URL
- Forwards request body as-is to local Ollama
- Streams SSE response back to hub in real-time

### 4. Response Streaming

- Reads SSE stream from Ollama line-by-line
- Processes event boundaries (blank lines)
- Sends chunks via `StreamInferenceResponse`
- Handles `[DONE]` termination token
- Marks final chunk with `IsFinal = true`

## SignalR Protocol

### Hub ? Worker

- `RequestInference(InferenceRequest)` - Process Ollama-format inference request
- `RequestOpenAI(InferenceRequest)` - Process OpenAI-format inference request
- `Registered()` - Confirmation of successful registration

### Worker ? Hub

- `RegisterNode(NodeRegistration)` - Register with available models
- `StreamInferenceResponse(InferenceChunk)` - Stream response chunk

## Ollama API Integration

The worker supports flexible parsing of Ollama's `/api/tags` endpoint:

### Format 1: Object with models array
```json
{
  "models": [
    {"name": "llama2"},
    {"model": "mistral"}
  ]
}
```

### Format 2: Direct array
```json
[
  {"name": "llama2"},
  "mistral"
]
```

### Format 3: String array
```json
["llama2", "mistral"]
```

## Dependencies

- **SignalR Client** (Microsoft.AspNetCore.SignalR.Client): Hub communication
- **Microsoft.Extensions.Hosting**: Background service hosting
- **GhubAi.ServiceDefaults**: Aspire service defaults
- **GhubAiShared**: Shared models and contracts

## Project Structure

```
GhubAiWorker/
??? Services/
?   ??? ProviderWorker.cs        # Main worker background service
??? Program.cs                   # Application entry point
```

## Error Handling

- **Connection Failures**: Automatic reconnection with 5-second delay
- **Request Errors**: Sends error message as final chunk
- **Parse Errors**: Logs warning and continues with empty model list
- **Stream Errors**: Catches and logs, sends error indication to hub

## Development Notes

### Certificate Validation

For local development with self-signed certificates:

```bash
export ALLOW_INVALID_CERTS=1
```

**?? WARNING**: Never use `ALLOW_INVALID_CERTS=1` in production!

### Supported Ollama Endpoints

The worker forwards to any endpoint URI provided in the request:
- `/api/generate` - Native Ollama generation
- `/api/chat` - Ollama chat format
- `/v1/chat/completions` - OpenAI-compatible format

### Reconnection Behavior

- Uses SignalR's `WithAutomaticReconnect()` for transient failures
- On connection closed, waits 5 seconds and attempts manual reconnection
- Continues model discovery during disconnection
- Automatically re-registers on successful reconnection

## Scaling

### Multiple Workers

Run multiple workers with the same hub:
- Each worker registers independently
- Hub distributes requests across workers with the requested model
- Workers can host different model sets
- Same model can be hosted by multiple workers for load distribution

### Resource Considerations

- Each worker requires Ollama running locally
- Memory requirements depend on loaded Ollama models
- One worker can handle concurrent requests if Ollama supports it

## Troubleshooting

### Worker won't connect
- Check `HUB_URL` is correct and hub is running
- Verify `PROVIDER_TOKEN` matches hub configuration
- For HTTPS issues, check certificate validity

### Models not discovered
- Ensure Ollama is running: `ollama list`
- Check `OLLAMA_LOCAL_URL` is correct
- Review logs for parsing errors

### Requests failing
- Verify Ollama endpoint URIs are correct
- Check Ollama logs for errors
- Ensure requested model is pulled in Ollama
