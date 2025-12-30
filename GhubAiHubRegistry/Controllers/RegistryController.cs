using GhubAiHubRegistry.Models;
using Microsoft.AspNetCore.Mvc;

namespace GhubAiHubRegistry.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RegistryController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public RegistryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet(Name = "Hubs")]
        public IEnumerable<HubInstance> Get()
        {
            var hubInstances = _configuration.GetSection("HubInstances").Get<HubInstance[]>();
            return hubInstances ?? Array.Empty<HubInstance>();
        }
    }
}
