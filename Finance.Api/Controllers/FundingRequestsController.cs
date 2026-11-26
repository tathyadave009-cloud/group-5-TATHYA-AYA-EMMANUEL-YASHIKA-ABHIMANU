using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

public record CreateFundingRequestRequest(
    string? DepartmentCode,
    decimal Amount,
    string? Reason,
    bool Urgent,
    string? RequisitionNumber = null,
    string? Role = null,
    int? Positions = null);

public record FundingDecisionRequest(bool Approved);

[ApiController]
[Route("api/budget/v1/funding-requests")]
public class FundingRequestsController : ControllerBase
{
    private readonly FundingRequestService _fundingRequestService;

    public FundingRequestsController(FundingRequestService fundingRequestService)
    {
        _fundingRequestService = fundingRequestService;
    }

    [HttpPost]
    public ActionResult<FundingRequest> Create(
        [FromBody] CreateFundingRequestRequest request)
    {
        try
        {
            var created = _fundingRequestService.Create(
                request.DepartmentCode,
                request.Amount,
                request.Reason,
                request.Urgent,
                request.RequisitionNumber,
                request.Role,
                request.Positions);

            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Error("bad_request", exception.Message));
        }
    }

    [HttpGet]
    public ActionResult<FundingRequest[]> Get()
    {
        return Ok(_fundingRequestService.GetAll());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<FundingRequest> GetById(Guid id)
    {
        var request = _fundingRequestService.GetAll()
            .FirstOrDefault(item => item.Id == id);

        if (request is null)
            return NotFound(Error("not_found", "Funding request was not found."));

        return Ok(request);
    }

    [HttpPut("{id:guid}/decision")]
    public async Task<ActionResult<FundingRequest>> DecideAsync(
        Guid id,
        [FromBody] FundingDecisionRequest request,
        [FromServices] HrClient hrClient,
        CancellationToken cancellationToken)
    {
        var result = _fundingRequestService.Decide(id, request.Approved);

        if (result.Outcome == FundingDecisionOutcome.Success &&
            result.Request is { } decided &&
            !string.IsNullOrWhiteSpace(decided.RequisitionNumber))
        {
            bool notified = await hrClient.TryNotifyDecisionAsync(
                decided.RequisitionNumber,
                request.Approved,
                cancellationToken);

            if (notified)
                result = result with { Request = _fundingRequestService.MarkHrNotified(decided) };
        }

        return DecisionResponse(result);
    }

    // Keeps existing direct controller tests working. This is not an HTTP endpoint.
    [NonAction]
    public ActionResult<FundingRequest> Decide(Guid id, FundingDecisionRequest request)
    {
        return DecisionResponse(_fundingRequestService.Decide(id, request.Approved));
    }

    private ActionResult<FundingRequest> DecisionResponse(FundingDecisionResult result)
    {
        return result.Outcome switch
        {
            FundingDecisionOutcome.Success => Ok(result.Request),
            FundingDecisionOutcome.NotFound => NotFound(Error("not_found", result.Message)),
            FundingDecisionOutcome.AlreadyDecided => Conflict(Error("conflict", result.Message)),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                Error("internal_error", "Unable to record the funding decision."))
        };
    }

    private ApiError Error(string code, string message)
    {
        return new ApiError(
            code,
            message,
            ControllerContext.HttpContext?.TraceIdentifier
                ?? Guid.NewGuid().ToString("N"));
    }
}