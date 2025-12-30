using GhubAiClientProxy.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register HttpClient for RegistryClient
builder.Services.AddHttpClient<IRegistryClient, RegistryClient>();

// Register DynamicProxyConfigProvider as singleton
builder.Services.AddSingleton<DynamicProxyConfigProvider>();

// Register the polling background service
builder.Services.AddHostedService<RegistryPollingService>();

// Add YARP reverse proxy with dynamic configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Use our dynamic config provider
builder.Services.AddSingleton<Yarp.ReverseProxy.Configuration.IProxyConfigProvider>(
    sp => sp.GetRequiredService<DynamicProxyConfigProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Map reverse proxy
app.MapReverseProxy();

app.Run();
