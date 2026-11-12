using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

public record HealthResponse(
    string Status,
    string Service,
    string Version);

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    [HttpGet("api/health")]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse("Healthy", "BudgetFinance", "0.2.0"));
    }
}
