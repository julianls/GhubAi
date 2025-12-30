using GhubAiHubRegistry.Models;
using Microsoft.AspNetCore.Mvc;

namespace GhubAiHubRegistry.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RegistryController : ControllerBase
    {
        [HttpGet(Name = "Hubs")]
        public IEnumerable<HubInstance> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new HubInstance
            {
                Address = "https://localhost:7197",
                Load = 1,
                Capacity = 100
            })
            .ToArray();
        }
    }
}
