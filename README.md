# GhubAi

A distributed AI inference system that enables load-balanced, scalable access to locally-hosted AI models through an OpenAI-compatible API. Built with .NET 10, SignalR, and YARP, GhubAi creates a grid computing architecture for AI workloads using Ollama as the inference backend.

## Overview

GhubAi (Grid Hub AI) is a microservices-based platform that coordinates multiple AI worker nodes to serve inference requests through a centralized hub and dynamic proxy. It provides OpenAI-compatible endpoints while leveraging local Ollama instances, making it ideal for:

- ?? Organizations wanting to self-host AI with multiple machines
- ?? Load balancing AI inference across multiple GPU servers
- ?? Scaling AI capabilities horizontally with commodity hardware
- ?? Drop-in replacement for OpenAI API with local models
- ?? Distributed AI inference without vendor lock-in

## Architecture

```
???????????????
?   Clients   ?
???????????????
       ?
       ?
????????????????????      ???????????????????
? GhubAiClientProxy???????? GhubAiHubRegistry?
?   (YARP Proxy)   ?      ?  (Service Disc.) ?
????????????????????      ???????????????????
         ?
         ?
??????????????????????
?    GhubAiHub       ????????
?  (Orchestration)   ?      ?
??????????????????????      ?
         ?                  ?
    ???????????            ?
    ?         ?            ?
??????????? ???????????   ? SignalR
? Worker  ? ? Worker  ?   ?
?  Node   ? ?  Node   ?   ?
??????????? ???????????   ?
     ?           ?         ?
     ?           ?         ?
  Ollama      Ollama       ?
  (Local)     (Local)      ?
                           ?
         More Workers ??????
```

## System Components

### ?? GhubAiClientProxy
**Dynamic reverse proxy and load balancer**

- Routes OpenAI-compatible `/v1/*` requests to healthy hubs
- Performs automatic health checking and failover
- Round-robin load balancing across hub instances
- Built with YARP (Yet Another Reverse Proxy)

[?? Read more](./GhubAiClientProxy/README.md)

### ?? GhubAiHub
**Central orchestration hub**

- Manages worker node registration via SignalR
- Routes inference requests to appropriate workers
- Provides OpenAI-compatible `/v1/chat/completions` endpoint
- Real-time streaming responses using Server-Sent Events
- Worker authentication and authorization

[?? Read more](./GhubAiHub/README.md)

### ?? GhubAiHubRegistry
**Service discovery registry**

- Maintains list of available hub instances
- Enables dynamic hub discovery for the proxy
- Simple REST API for querying hub endpoints
- Configuration-based hub management

[?? Read more](./GhubAiHubRegistry/README.md)

### ?? GhubAiWorker
**AI inference worker node**

- Connects to hub via SignalR
- Auto-discovers local Ollama models
- Processes inference requests and streams responses
- Automatic reconnection and fault tolerance
- Supports multiple Ollama API formats

[?? Read more](./GhubAiWorker/README.md)

### ?? GhubAiShared
**Shared contracts and models**

- Common data models and contracts
- SignalR message definitions
- Type-safe communication between components

### ?? GhubAi.ServiceDefaults
**Aspire service defaults**

- OpenTelemetry integration
- Health checks
- Service discovery
- Consistent configuration across services

### ?? GhubAi.AppHost
**.NET Aspire orchestration**

- Local development orchestration
- Service dependency management
- Simplified multi-service debugging
- Health check monitoring

## Key Features

### For Users
- ? **OpenAI-Compatible API**: Drop-in replacement for OpenAI client libraries
- ? **Streaming Responses**: Real-time token streaming via SSE
- ? **Automatic Model Discovery**: Workers auto-detect available Ollama models
- ? **Load Balancing**: Requests distributed across available workers
- ? **High Availability**: Automatic failover and health checking

### For Operators
- ? **Horizontal Scaling**: Add workers dynamically without reconfiguration
- ? **Multi-Model Support**: Different workers can host different model sets
- ? **Observability**: Built-in OpenTelemetry and health checks
- ? **Fault Tolerance**: Automatic reconnection and error handling
- ? **.NET Aspire Ready**: Modern cloud-native development experience

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Ollama installed on worker nodes
- Docker (optional, for containerized deployment)

### Quick Start with .NET Aspire

1. **Clone the repository**
   ```bash
   git clone https://github.com/julianls/GhubAi.git
   cd GhubAi
   ```

2. **Pull Ollama models** (on worker machine)
   ```bash
   ollama pull llama2
   ollama pull mistral
   ```

3. **Run with Aspire**
   ```bash
   dotnet run --project GhubAi.AppHost
   ```

4. **Make inference requests**
   ```bash
   curl http://localhost:5000/v1/chat/completions \
     -H "Content-Type: application/json" \
     -d '{
       "model": "llama2",
       "messages": [{"role": "user", "content": "Hello!"}],
       "stream": true
     }'
   ```

### Manual Deployment

#### 1. Start the Registry
```bash
cd GhubAiHubRegistry
dotnet run
# Runs on https://localhost:7001
```

#### 2. Start the Hub
```bash
cd GhubAiHub
dotnet run
# Runs on https://localhost:7197
```

#### 3. Start Workers
```bash
cd GhubAiWorker
export HUB_URL=https://localhost:7197/gridhub
export OLLAMA_LOCAL_URL=http://localhost:11434
export PROVIDER_TOKEN=your-secure-token
dotnet run
```

#### 4. Start the Proxy
```bash
cd GhubAiClientProxy
dotnet run
# Runs on https://localhost:5000
```

## Configuration

### Environment Variables

**GhubAiWorker**
```bash
HUB_URL=https://hub.example.com/gridhub
PROVIDER_TOKEN=your-secure-token
OLLAMA_LOCAL_URL=http://localhost:11434
ALLOW_INVALID_CERTS=0  # Set to 1 for dev only
```

**GhubAiHub**
```json
{
  "ProviderTokens": ["token1", "token2"]
}
```

**GhubAiHubRegistry**
```json
{
  "HubInstances": [
    {"Name": "Hub1", "Address": "https://hub1.example.com"},
    {"Name": "Hub2", "Address": "https://hub2.example.com"}
  ]
}
```

**GhubAiClientProxy**
```json
{
  "RegistryUrl": "https://localhost:7001"
}
```

## API Usage

### OpenAI-Compatible Endpoints

**Chat Completions (Streaming)**
```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "llama2",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Explain quantum computing"}
    ],
    "stream": true
  }'
```

**Chat Completions (Non-streaming)**
```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "mistral",
    "messages": [{"role": "user", "content": "Hello!"}],
    "stream": false
  }'
```

### Using with OpenAI Client Libraries

**Python**
```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:5000/v1",
    api_key="not-needed"  # API key not required
)

response = client.chat.completions.create(
    model="llama2",
    messages=[{"role": "user", "content": "Hello!"}],
    stream=True
)

for chunk in response:
    print(chunk.choices[0].delta.content, end="")
```

**JavaScript/TypeScript**
```typescript
import OpenAI from 'openai';

const client = new OpenAI({
  baseURL: 'http://localhost:5000/v1',
  apiKey: 'not-needed'
});

const stream = await client.chat.completions.create({
  model: 'llama2',
  messages: [{ role: 'user', content: 'Hello!' }],
  stream: true,
});

for await (const chunk of stream) {
  process.stdout.write(chunk.choices[0]?.delta?.content || '');
}
```

## Project Structure

```
GhubAi/
??? GhubAi.AppHost/              # .NET Aspire orchestration
??? GhubAi.ServiceDefaults/      # Shared Aspire service defaults
??? GhubAiClientProxy/           # YARP reverse proxy
?   ??? Models/
?   ??? Services/
?   ??? README.md
??? GhubAiHub/                   # Central orchestration hub
?   ??? Controllers/
?   ??? Hubs/
?   ??? Services/
?   ??? Helpers/
?   ??? README.md
??? GhubAiHubRegistry/           # Service discovery registry
?   ??? Controllers/
?   ??? Models/
?   ??? README.md
??? GhubAiWorker/                # Worker node
?   ??? Services/
?   ??? README.md
??? GhubAiShared/                # Shared contracts
?   ??? Contracts.cs
??? GhubAiClientProxy.Tests/     # Proxy unit tests
??? GhubAiHub.Tests/             # Hub unit tests
??? GhubAiWorker.Tests/          # Worker unit tests
??? GhubAiShared.Tests/          # Shared library tests
```

## Technology Stack

- **.NET 10.0**: Modern cross-platform framework
- **ASP.NET Core**: Web APIs and middleware
- **SignalR**: Real-time bi-directional communication
- **YARP**: High-performance reverse proxy
- **.NET Aspire**: Cloud-native orchestration and observability
- **OpenTelemetry**: Distributed tracing and metrics
- **Ollama**: Local LLM inference engine

## Use Cases

### 1. GPU Server Pool
Distribute inference across multiple GPU-equipped machines in your datacenter.

### 2. Hybrid Cloud Setup
Mix cloud VMs and on-premise hardware in a single inference grid.

### 3. Development Teams
Share expensive GPU resources across development teams with automatic load balancing.

### 4. Cost Optimization
Replace expensive API calls with self-hosted models on commodity hardware.

### 5. Privacy & Compliance
Keep sensitive data on-premise while maintaining OpenAI-compatible APIs.

## Performance Considerations

- **Concurrency**: Each worker can handle concurrent requests based on Ollama configuration
- **Model Loading**: First request to a model may be slower due to loading time
- **Network Latency**: SignalR adds minimal overhead (~1-5ms) for grid coordination
- **Streaming**: Response streaming minimizes time-to-first-token
- **Scale Out**: Add more workers linearly to increase capacity

## Development

### Running Tests
```bash
dotnet test
```

### Building
```bash
dotnet build
```

### Running Individual Services
```bash
dotnet run --project GhubAi.AppHost
# Or run services individually for debugging
```

## Roadmap

- [ ] **Authentication & Authorization**: Add API key support and user management
- [ ] **Rate Limiting**: Per-user/per-model rate limiting
- [ ] **Model Caching**: Intelligent model loading and unloading
- [ ] **Advanced Routing**: Cost-based, latency-based routing strategies
- [ ] **Monitoring Dashboard**: Real-time system health and metrics visualization
- [ ] **Multi-Hub Federation**: Support for multiple interconnected hub clusters
- [ ] **Dynamic Model Discovery**: Hub-level model registry aggregation
- [ ] **Queue Management**: Request queuing during high load
- [ ] **Model Warm-up**: Pre-load frequently used models
- [ ] **Embeddings Support**: Add `/v1/embeddings` endpoint

## Security Considerations

### Production Deployment

- ? Use HTTPS for all communications
- ? Configure `PROVIDER_TOKEN` for worker authentication
- ? Never set `ALLOW_INVALID_CERTS=1` in production
- ? Implement network segmentation (workers in private network)
- ? Use API gateway with authentication for public access
- ? Regular security updates for dependencies
- ? Rate limiting and DDoS protection at proxy layer

## Contributing

Contributions are welcome! Please feel free to submit issues, fork the repository, and create pull requests.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is open source. Please check the repository for license details.

## Acknowledgments

- Built with [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- Powered by [Ollama](https://ollama.ai/) for local AI inference
- Reverse proxy via [YARP](https://microsoft.github.io/reverse-proxy/)
- Real-time communication with [SignalR](https://dotnet.microsoft.com/apps/aspnet/signalr)

## Support

- ?? **Issues**: Report bugs via [GitHub Issues](https://github.com/julianls/GhubAi/issues)
- ?? **Discussions**: Join conversations in GitHub Discussions
- ?? **Contact**: For security issues, contact the maintainers directly

---

**Built with ?? using .NET 10 and .NET Aspire**
