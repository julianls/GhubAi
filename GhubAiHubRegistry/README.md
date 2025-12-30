# GhubAiHubRegistry

A lightweight registry service that maintains and serves a list of available GhubAiHub instances. Used by the ClientProxy to discover hub endpoints for load balancing.

## Overview

GhubAiHubRegistry acts as a service discovery endpoint for the GhubAi distributed system. It provides a simple HTTP API that returns configured hub instances, allowing the ClientProxy to dynamically discover and route to available hubs.

## Features

- **Service Discovery**: Centralized registry of hub instances
- **Simple Configuration**: Hub list defined in `appsettings.json`
- **REST API**: Clean HTTP endpoint for querying available hubs
- **Aspire Integration**: Built-in OpenTelemetry, health checks, and service discovery
- **OpenAPI Support**: Automatic API documentation in development mode

## Architecture

```
GhubAiClientProxy --[polls]--> GhubAiHubRegistry
                                     |
                              [returns hub list]
                                     |
                              Configuration File
```

## API Endpoints

### Registry

- `GET /Registry` - Returns array of available hub instances

Response format:
```json
[
  {
    "name": "Hub1",
    "address": "https://hub1.example.com"
  },
  {
    "name": "Hub2",
    "address": "https://hub2.example.com"
  }
]
```

### Health Checks

- `GET /health` - Standard Aspire health check endpoint

### OpenAPI

- `GET /openapi/v1.json` - OpenAPI specification (development mode only)

## Configuration

Hub instances are configured in `appsettings.json`:

```json
{
  "HubInstances": [
    {
      "Name": "Hub-Primary",
      "Address": "https://hub1.example.com"
    },
    {
      "Name": "Hub-Secondary",
      "Address": "https://hub2.example.com"
    },
    {
      "Name": "Hub-Tertiary",
      "Address": "https://hub3.example.com"
    }
  ]
}
```

### HubInstance Model

```csharp
public class HubInstance
{
    public string Name { get; set; }
    public string Address { get; set; }
}
```

## Running the Service

### Prerequisites

- .NET 10.0 SDK

### Local Development

```bash
dotnet run
```

The registry will start and serve the configured hub list on the default port.

### Docker

```bash
docker build -t ghubai-registry .
docker run -p 7001:7001 \
  -e HubInstances__0__Name=Hub1 \
  -e HubInstances__0__Address=https://hub1.example.com \
  ghubai-registry
```

## Dependencies

- **ASP.NET Core**: Web framework
- **GhubAi.ServiceDefaults**: Aspire service defaults

## Project Structure

```
GhubAiHubRegistry/
??? Controllers/
?   ??? RegistryController.cs    # Registry API endpoint
??? Models/
?   ??? HubInstance.cs           # Hub instance model
??? Program.cs                   # Application entry point
```

## How It Works

1. **Configuration Loading**: On startup, reads `HubInstances` from configuration
2. **API Serving**: Exposes HTTP endpoint returning the hub list
3. **Client Polling**: ClientProxy periodically polls this endpoint
4. **Dynamic Discovery**: Clients discover hubs without hardcoded addresses

## Use Cases

### Static Hub List

For environments with fixed hub instances, simply configure them in `appsettings.json`.

### Dynamic Registration (Future)

The registry could be extended to support:
- Hub self-registration via POST endpoints
- Health status tracking
- Automatic hub removal on failure
- Load metrics for intelligent routing

## Integration

The registry is consumed by GhubAiClientProxy:

```csharp
var hubs = await registryClient.GetHubInstancesAsync();
foreach (var hub in hubs)
{
    // Health check and add to proxy configuration
}
```

## Development Notes

- Returns empty array if no hubs are configured
- Does not perform health checks (delegated to ClientProxy)
- Configuration changes require service restart
- Consider implementing dynamic registration for production scenarios
- Can be deployed as a shared service or per-environment

## Security Considerations

- In production, implement authentication for registry access
- Use HTTPS for all communications
- Consider rate limiting for the registry endpoint
- Validate hub addresses before returning to clients
