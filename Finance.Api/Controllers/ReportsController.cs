using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

/// <summary>
/// BF-24: financial reports for a date range.
/// </summary>
[ApiController]
[Route("api/budget/v1/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportsController(ExpenseService expenseService, RevenueService revenueService)
    {
        _reportService = new ReportService(expenseService, revenueService);
    }

    /// <summary>
    /// GET /api/budget/v1/reports/summary?from=2026-11-01&amp;to=2026-11-30
    /// Spending per department, revenue per source and the net position.
    /// </summary>
    /// <response code="200">The report.</response>
    /// <response code="400">from or to is missing, or from is after to.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<ReportSummary> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        string? traceId = HttpContext?.TraceIdentifier;

        if (from is null || to is null)
        {
            return BadRequest(ApiError.BadRequest(
                "Both from and to are required, as dates like 2026-11-01.", traceId));
        }

        if (from.Value.Date > to.Value.Date)
        {
            return BadRequest(ApiError.BadRequest(
                "from must be on or before to.", traceId));
        }

        return Ok(_reportService.Build(from.Value, to.Value));
    }
}
