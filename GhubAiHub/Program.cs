var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddSingleton<GhubAiHub.Services.NodeRegistry>();
builder.Services.AddSingleton<GhubAiHub.Services.ResponseManager>();

builder.Services.AddSignalR();

// Register provider token authentication scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "ProviderToken";
    options.DefaultChallengeScheme = "ProviderToken";
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, GhubAiHub.Services.ProviderTokenAuthenticationHandler>(
    "ProviderToken", options => { });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GhubAiHub.Hubs.GridHub>("/gridhub");

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
