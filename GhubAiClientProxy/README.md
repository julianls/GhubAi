# GhubAiClientProxy

A dynamic reverse proxy service that routes OpenAI-compatible API requests to healthy GhubAi hub instances using YARP (Yet Another Reverse Proxy).

## Overview

GhubAiClientProxy acts as a load-balanced gateway for AI inference requests. It continuously polls the HubRegistry to discover available hub instances, performs health checks, and dynamically updates routing configuration to distribute traffic only to healthy hubs.

## Features

- **Dynamic Configuration**: Automatically discovers and routes to available hub instances
- **Health Checking**: Continuously monitors hub health and excludes unhealthy instances
- **Load Balancing**: Round-robin distribution across healthy hubs
- **OpenAI API Compatibility**: Routes `/v1/*` endpoints to backend hubs
- **Aspire Integration**: Built-in OpenTelemetry, health checks, and service discovery

## Architecture

```
Client -> GhubAiClientProxy -> [Hub1, Hub2, Hub3...] -> Workers
              ?
              |
       HubRegistry (polling)
```

## Components

### Services

- **DynamicProxyConfigProvider**: Implements YARP's `IProxyConfigProvider` to dynamically update routing configuration
- **RegistryPollingService**: Background service that polls the HubRegistry every 30 seconds to refresh hub list
- **RegistryClient**: HTTP client wrapper for communicating with the HubRegistry

## Configuration

### Environment Variables

The proxy relies on configuration from `appsettings.json` or environment variables:

```json
{
  "RegistryUrl": "https://localhost:7001"
}
```

### Reverse Proxy

YARP configuration is dynamically generated but can be initialized from `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Routes": {},
    "Clusters": {}
  }
}
```

## API Endpoints

### Proxied Routes

- `GET/POST /v1/**` - All OpenAI-compatible API endpoints are proxied to backend hubs

### Health Checks

- `GET /health` - Standard Aspire health check endpoint

## Running the Service

### Prerequisites

- .NET 10.0 SDK
- Access to a running HubRegistry instance

### Local Development

```bash
dotnet run
```

### Docker

```bash
docker build -t ghubai-clientproxy .
docker run -p 8080:8080 -e RegistryUrl=https://registry:7001 ghubai-clientproxy
```

## Dependencies

- **YARP** (Microsoft.ReverseProxy): Reverse proxy framework
- **GhubAi.ServiceDefaults**: Aspire service defaults
- **GhubAiShared**: Shared models and contracts

## Project Structure

```
GhubAiClientProxy/
??? Models/
?   ??? HubInstance.cs           # Hub instance model
??? Services/
?   ??? DynamicProxyConfigProvider.cs  # YARP dynamic config
?   ??? RegistryPollingService.cs      # Background polling service
?   ??? RegistryClient.cs              # Registry HTTP client
?   ??? IRegistryClient.cs             # Registry client interface
??? Program.cs                   # Application entry point
```

## How It Works

1. **Startup**: The proxy starts and registers with YARP using `DynamicProxyConfigProvider`
2. **Discovery**: `RegistryPollingService` polls the HubRegistry every 30 seconds
3. **Health Checks**: Each discovered hub is health-checked at `/health`
4. **Configuration Update**: Only healthy hubs are added to the routing configuration
5. **Request Routing**: Incoming `/v1/*` requests are round-robin distributed to healthy hubs
6. **Dynamic Updates**: Configuration automatically updates when hubs become available or unavailable

## Development Notes

- The proxy filters routes to only OpenAI-compatible endpoints (`/v1/*`)
- Health checks timeout after 5 seconds
- Unhealthy hubs are excluded from routing but re-checked on the next poll cycle
- Configuration changes trigger YARP to reload without restarting the service
