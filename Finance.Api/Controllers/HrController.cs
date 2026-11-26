using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

// BF-30: HR payroll on our side, for the HR Payroll section of the Finance page (HR-C19).
[ApiController]
[Route("api/budget/v1/hr")]
public class HrController : ControllerBase
{
    private readonly HrClient _hrClient;

    public HrController(HrClient hrClient)
    {
        _hrClient = hrClient;
    }

    // GET /api/budget/v1/hr/payroll
    // 200 = HR's headcount and salary totals per department (passed through as HR sends them)
    // 503 = HR is not configured, down or too slow, in the class error format
    [HttpGet("payroll")]
    public async Task<IActionResult> GetPayrollAsync(CancellationToken cancellationToken)
    {
        var payroll = await _hrClient.TryGetPayrollAsync(cancellationToken);

        if (payroll is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError(
                "service_unavailable",
                "HR service unavailable. Try again in a moment.",
                HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("n")));
        }

        return Ok(payroll.Value);
    }
}
