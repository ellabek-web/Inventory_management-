using Microsoft.AspNetCore.Mvc;
namespace InventorySystem.Backend.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy", service = "InventorySystem.Backend", utcTime = DateTimeOffset.UtcNow });
}
