var builder = WebApplication.CreateBuilder(args);

// Enable Windows Service and Linux Systemd support
builder.Host.UseWindowsService();
builder.Host.UseSystemd();

// Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Map default endpoints (health checks)
app.MapDefaultEndpoints();

app.Run();
