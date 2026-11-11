using Finance.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

public record CreateFundingRequestRequest(
    string? DepartmentCode,
    decimal Amount,
    string? Reason,
    bool Urgent);

public record FundingDecisionRequest(bool Approved);

[ApiController]
[Route("api/budget/v1/funding-requests")]
public class FundingRequestsController : ControllerBase
{
    private readonly FundingRequestService _fundingRequestService;

    public FundingRequestsController(
        FundingRequestService fundingRequestService)
    {
        _fundingRequestService = fundingRequestService;
    }

    [HttpPost]
    public ActionResult<FundingRequest> Create(
        [FromBody] CreateFundingRequestRequest request)
    {
        try
        {
            FundingRequest created =
                _fundingRequestService.Create(
                    request.DepartmentCode,
                    request.Amount,
                    request.Reason,
                    request.Urgent);

            return StatusCode(
                StatusCodes.Status201Created,
                created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpGet]
    public ActionResult<FundingRequest[]> Get()
    {
        return Ok(_fundingRequestService.GetAll());
    }

    [HttpPut("{id:guid}/decision")]
    public ActionResult<FundingRequest> Decide(
        Guid id,
        [FromBody] FundingDecisionRequest request)
    {
        FundingDecisionResult result =
            _fundingRequestService.Decide(
                id,
                request.Approved);

        if (result.Outcome == FundingDecisionOutcome.Success)
        {
            return Ok(result.Request);
        }

        if (result.Outcome == FundingDecisionOutcome.NotFound)
        {
            return NotFound(new
            {
                message = result.Message
            });
        }

        if (result.Outcome ==
            FundingDecisionOutcome.AlreadyDecided)
        {
            return Conflict(new
            {
                message = result.Message
            });
        }

        return StatusCode(
            StatusCodes.Status500InternalServerError);
    }
}