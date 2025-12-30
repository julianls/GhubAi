# GhubAiHub

The central coordination hub for the GhubAi distributed AI inference system. Manages worker node registration, routes inference requests, and provides OpenAI-compatible API endpoints.

## Overview

GhubAiHub acts as the orchestration layer between clients and AI worker nodes. It maintains a registry of connected workers and their available models, routes inference requests to appropriate workers via SignalR, and streams responses back to clients.

## Features

- **Worker Management**: Real-time registration and tracking of worker nodes via SignalR
- **Model Discovery**: Maintains an up-to-date registry of available models across workers
- **Request Routing**: Intelligently routes inference requests to workers hosting the requested model
- **Streaming Responses**: Real-time streaming of AI inference responses
- **OpenAI API Compatibility**: Provides `/v1/chat/completions` endpoint
- **Provider Authentication**: Token-based authentication for worker nodes
- **Aspire Integration**: Built-in OpenTelemetry, health checks, and service discovery

## Architecture

```
Client -> AIController -> GridHub -> Workers (via SignalR)
                            ?
                       NodeRegistry
                            ?
                     ResponseManager
```

## Components

### Controllers

- **AIController**: Handles HTTP requests for AI inference (`/v1/chat/completions`)
- **RegistryController**: Exposes the node registry for health monitoring

### Hubs

- **GridHub**: SignalR hub managing worker connections and bi-directional communication

### Services

- **NodeRegistry**: Thread-safe registry of connected worker nodes and their capabilities
- **ResponseManager**: Manages streaming responses from workers back to clients
- **ProviderTokenAuthenticationHandler**: Custom authentication handler for worker node tokens

## API Endpoints

### AI Inference

- `POST /v1/chat/completions` - OpenAI-compatible chat completions endpoint
  - Supports streaming responses via Server-Sent Events (SSE)
  - Automatically routes to workers with the requested model

### Registry

- `GET /api/registry` - Returns list of connected workers and their available models

### Health Checks

- `GET /health` - Standard Aspire health check endpoint

## Configuration

### Environment Variables

Configure via `appsettings.json` or environment variables:

```json
{
  "ProviderTokens": ["your-secure-token-1", "your-secure-token-2"]
}
```

### SignalR Hub

Workers connect to the SignalR hub at:
```
wss://your-hub-address/gridhub
```

## Running the Service

### Prerequisites

- .NET 10.0 SDK
- At least one GhubAiWorker instance configured to connect

### Local Development

```bash
dotnet run
```

The hub will start and listen for worker connections. Workers must provide valid authentication tokens.

### Docker

```bash
docker build -t ghubai-hub .
docker run -p 7197:7197 ghubai-hub
```

## SignalR Protocol

### Worker ? Hub

- `RegisterNode(NodeRegistration)` - Register/update worker with available models
- `StreamInferenceResponse(InferenceChunk)` - Stream inference response chunks

### Hub ? Worker

- `RequestInference(InferenceRequest)` - Request Ollama API inference
- `RequestOpenAI(InferenceRequest)` - Request OpenAI-format inference
- `Registered()` - Confirmation of successful registration

## Dependencies

- **SignalR** (Microsoft.AspNetCore.SignalR): Real-time bi-directional communication
- **GhubAi.ServiceDefaults**: Aspire service defaults
- **GhubAiShared**: Shared models and contracts

## Project Structure

```
GhubAiHub/
??? Controllers/
?   ??? AIController.cs              # AI inference HTTP endpoints
?   ??? RegistryController.cs        # Registry API
??? Hubs/
?   ??? GridHub.cs                   # SignalR hub for workers
??? Services/
?   ??? NodeRegistry.cs              # Worker registry
?   ??? ResponseManager.cs           # Response streaming manager
?   ??? ProviderTokenAuthenticationHandler.cs  # Worker auth
??? Helpers/
?   ??? StreamSseResult.cs           # SSE streaming helper
??? Program.cs                       # Application entry point
```

## How It Works

1. **Worker Registration**: Workers connect via SignalR and send `RegisterNode` with available models
2. **Request Handling**: Client POSTs to `/v1/chat/completions` with model and messages
3. **Routing**: Hub looks up workers hosting the requested model
4. **Dispatch**: Request is forwarded to selected worker via `RequestInference` or `RequestOpenAI`
5. **Streaming**: Worker streams response chunks via `StreamInferenceResponse`
6. **Response**: Hub forwards chunks to client using SSE

## Authentication

Workers must authenticate using the `ProviderToken` authentication scheme:

```csharp
options.AccessTokenProvider = () => Task.FromResult("your-token");
```

Tokens are validated against the configured `ProviderTokens` list.

## Development Notes

- Node registry automatically cleans up disconnected workers
- Responses are streamed using Server-Sent Events (SSE) format
- The hub supports both Ollama-native and OpenAI-format requests
- Heartbeat timestamps track worker availability
- Multiple workers can host the same model for load distribution
