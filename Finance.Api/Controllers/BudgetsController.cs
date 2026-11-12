using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/budgets")]
public class BudgetsController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public BudgetsController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    // GET /api/budgets -> all 7 departments
    [HttpGet]
    public ActionResult<IEnumerable<Budget>> GetAll()
    {
        var budgets = DepartmentBudgets.All.Select(ToBudget).ToList();
        return Ok(budgets);
    }

    // BF-13: GET /api/budgets/summary -> totals for the whole city
    [HttpGet("summary")]
    public ActionResult<BudgetSummary> GetSummary()
    {
        var budgets = DepartmentBudgets.All.Select(ToBudget).ToList();

        var summary = new BudgetSummary(
            budgets.Sum(b => b.Allocated),
            budgets.Sum(b => b.Spent),
            budgets.Sum(b => b.Remaining),
            "CAD",
            budgets.Where(b => b.LowBudget).Select(b => b.DepartmentCode).ToList());

        return Ok(summary);
    }

    // GET /api/budgets/EMS -> one department
    [HttpGet("{departmentCode}")]
    public ActionResult<Budget> Get(string departmentCode)
    {
        var department = DepartmentBudgets.Find(departmentCode);
        if (department == null)
            return NotFound(Error("not_found", $"Unknown department code '{departmentCode}'."));

        return Ok(ToBudget(department));
    }

    // BF-06: PUT /api/budgets/EMS  body: { "allocated": 5000000 }
    // 200 = changed, 404 = unknown department, 400 = refused (e.g. lower than spent)
    [HttpPut("{departmentCode}")]
    public ActionResult<Budget> Update(string departmentCode, [FromBody] UpdateBudgetRequest request)
    {
        var department = DepartmentBudgets.Find(departmentCode);
        if (department == null)
            return NotFound(Error("not_found", $"Unknown department code '{departmentCode}'."));

        var result = _expenseService.SetAllocation(department.Code, request.Allocated);
        if (!result.Success)
            return BadRequest(Error("invalid_budget", result.Message));

        return Ok(ToBudget(department));
    }

    // Allocated and remaining come from ExpenseService, so changes from
    // BF-06 (new budget), BF-07 (approved funding) and expenses all show here.
    private Budget ToBudget(Department department)
    {
        decimal allocated = _expenseService.GetAllocated(department.Code) ?? department.Allocated;
        decimal remaining = _expenseService.GetRemainingBudget(department.Code) ?? allocated;
        decimal spent = allocated - remaining;
        bool lowBudget = remaining < allocated * 0.10m;

        return new Budget(department.Code, department.Name, allocated, spent, remaining, lowBudget);
    }

    // Class error shape (SH-I03): { error, message, traceId }
    private object Error(string code, string message) => new
    {
        error = code,
        message,
        traceId = HttpContext?.TraceIdentifier ?? ""
    };
}
