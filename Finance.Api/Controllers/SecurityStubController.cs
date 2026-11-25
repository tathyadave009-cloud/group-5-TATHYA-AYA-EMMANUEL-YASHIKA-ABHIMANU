
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

// Simulates the Security service for BF-20 integration testing.
[ApiController]
[Route("api/budget/v1/security-stub")]
public class SecurityStubController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityStubController> _logger;

    public SecurityStubController(
        IConfiguration configuration,
        ILogger<SecurityStubController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("overdue-notice")]
    public IActionResult ReceiveNotice(
        [FromBody] OverdueFineNotice notice)
    {
        if (!_configuration.GetValue<bool>("Security:StubEnabled"))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiError(
                    "service_unavailable",
                    "Security stub is disabled.",
                    HttpContext.TraceIdentifier));
        }

        _logger.LogInformation(
            "Security stub received overdue notice for fine {FineId}",
            notice.FineId);

        return Ok(new
        {
            received = true,
            fineId = notice.FineId,
            message = "Overdue fine notice received."
        });
    }
}
