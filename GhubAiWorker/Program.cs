using GhubAiWorker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Enable Windows Service and Linux Systemd support
builder.Services.AddWindowsService();
builder.Services.AddSystemd();

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

builder.Services.AddHostedService<ProviderWorker>();

var host = builder.Build();

await host.RunAsync();
