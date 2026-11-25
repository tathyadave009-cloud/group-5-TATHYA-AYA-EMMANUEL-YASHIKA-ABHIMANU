
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
namespace Finance.Api.Controllers;

// Request body for recording city revenue.
public record CreateRevenueRequest(
    string? Source,
    string? DepartmentCode,
    decimal Amount,
    string? Description,
    string? ResidentId = null,
    DateOnly? DueDate = null);

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
            request.Description,
            request.ResidentId,
            request.DueDate);

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

    // BF-20: Send an overdue fine notice to Security.
    [HttpPost("{id:guid}/overdue-notice")]
    public async Task<IActionResult> SendOverdueNotice(
        Guid id,
        [FromServices] SecurityNotificationService securityService)
    {
        string? traceId = HttpContext?.TraceIdentifier;

        var revenue = _revenueService.GetRevenueById(id);

        if (revenue is null)
        {
            return NotFound(ApiError.NotFound(
                "Revenue record not found.", traceId));
        }

        if (revenue.Source != "FINES")
        {
            return BadRequest(ApiError.BadRequest(
                "Overdue notices are only available for fines.",
                traceId));
        }

        if (string.IsNullOrWhiteSpace(revenue.ResidentId) ||
            revenue.DueDate is null)
        {
            return BadRequest(ApiError.BadRequest(
                "Resident ID and due date are required.",
                traceId));
        }

        if (revenue.DueDate.Value >=
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(ApiError.BadRequest(
                "The fine is not overdue.", traceId));
        }

        if (revenue.OverdueNoticeStatus == "Sent")
        {
            return Conflict(ApiError.Conflict(
                "An overdue notice has already been sent.",
                traceId));
        }

        var notice = new OverdueFineNotice(
            revenue.ResidentId,
            revenue.Id,
            revenue.Amount,
            revenue.DueDate.Value,
            "Overdue fine");

        string status =
            await securityService.SendOverdueNoticeAsync(notice);

        _revenueService.UpdateOverdueNoticeStatus(id, status);

        return Ok(new
        {
            fineId = id,
            status
        });
    }
}
