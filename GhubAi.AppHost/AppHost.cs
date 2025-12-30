using Google.Protobuf.WellKnownTypes;

var builder = DistributedApplication.CreateBuilder(args);

var registry = builder.AddProject<Projects.GhubAiHubRegistry>("registry")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var hub = builder.AddProject<Projects.GhubAiHub>("hub")
    .WithReference(registry)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var proxy = builder.AddProject<Projects.GhubAiClientProxy>("proxy")
    .WithReference(hub)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WaitFor(hub);

var worker = builder.AddProject<Projects.GhubAiWorker>("worker")
    .WithReference(registry)
    .WithReference(hub);

builder.Build().Run();
