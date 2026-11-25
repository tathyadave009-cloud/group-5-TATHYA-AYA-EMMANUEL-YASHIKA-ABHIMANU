using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
namespace Finance.Api.Controllers;

// Request body for recording city revenue.
public record CreateRevenueRequest(
    string? Source,
    string? DepartmentCode,
    decimal Amount,
    string? Description);

[ApiController]
[Route("api/budget/v1/revenue")]
public class RevenueController : ControllerBase
{
    private readonly RevenueService _revenueService;

    public RevenueController(RevenueService revenueService)
    {
        _revenueService = revenueService;
    }

    // POST /api/budget/v1/revenue
    [HttpPost]
    [ProducesResponseType(typeof(RevenueResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<RevenueResult> Post(
        [FromBody] CreateRevenueRequest request)
    {
        RevenueResult result = _revenueService.RecordRevenue(
            request.Source,
            request.DepartmentCode,
            request.Amount,
            request.Description);

        if (!result.Approved)
        {
            return BadRequest(ApiError.BadRequest(
                result.Message,
                HttpContext?.TraceIdentifier));
        }

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    // GET /api/budget/v1/revenue
    [HttpGet]
    public ActionResult<Revenue[]> Get()
    {
        Revenue[] revenues = _revenueService.GetRevenues();

        return Ok(revenues);
    }

    // GET /api/budget/v1/revenue/summary
    [HttpGet("summary")]
    public ActionResult<Dictionary<string, decimal>> GetSummary()
    {
        Dictionary<string, decimal> summary =
            _revenueService.GetRevenueSummary();

        return Ok(summary);
    }
}
