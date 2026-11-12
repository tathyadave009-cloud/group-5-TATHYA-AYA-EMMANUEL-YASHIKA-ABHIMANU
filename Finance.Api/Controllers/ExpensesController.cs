using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

public record CreateExpenseRequest(
    string? DepartmentCode,
    decimal Amount,
    string? Description);

[ApiController]
[Route("api/budget/v1/expenses")]
[Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public ExpensesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpPost]
    public ActionResult<ExpenseResult> Post(
        [FromBody] CreateExpenseRequest request)
    {
        ExpenseResult result = _expenseService.RecordExpense(
            request.DepartmentCode,
            request.Amount,
            request.Description);

        if (!result.Approved)
        {
            return BadRequest(result);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    [HttpGet]
    public ActionResult<ExpensePage> Get(
        [FromQuery] string? department = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        try
        {
            ExpensePage result = _expenseService.GetExpenses(
                department,
                page,
                pageSize);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ApiError(
                "bad_request",
                exception.Message,
                HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("n")));
        }
    }
}
