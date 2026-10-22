using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

public record CreateExpenseRequest(
    string? DepartmentCode,
    decimal Amount,
    string? Description);

[ApiController]
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
        var result = _expenseService.RecordExpense(
            request.DepartmentCode,
            request.Amount,
            request.Description);

        if (!result.Approved)
            return BadRequest(result);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}