using GhubAiHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace GhubAiHub.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RegistryController : ControllerBase
{
    private readonly NodeRegistry _registry;
    public RegistryController(NodeRegistry registry)
    {
        _registry = registry;
    }

    // GET /api/tags - return list of available models across the pool
    [HttpGet("tags")]
    public IActionResult GetTags()
    {
        var models = _registry.GetAll().SelectMany(n => n.HostedModels).Distinct(StringComparer.OrdinalIgnoreCase);
        // Return simple array of model names to mimic Ollama's tags endpoint
        return Ok(models);
    }

    // GET /api/registry - return detailed node registry information
    [HttpGet("registry")]
    public IActionResult GetRegistry()
    {
        var nodes = _registry.GetAll().Select(n => new
        {
            connectionId = n.ConnectionId,
            machineName = n.MachineName,
            hostedModels = n.HostedModels.OrderBy(m => m).ToArray(),
            currentLoad = n.CurrentLoad,
            lastHeartbeat = n.LastHeartbeat
        });

        return Ok(new { nodes = nodes });
    }

}
