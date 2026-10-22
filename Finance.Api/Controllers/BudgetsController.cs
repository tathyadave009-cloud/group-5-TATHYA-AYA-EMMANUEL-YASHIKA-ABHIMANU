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

    // GET /api/budgets/EMS -> one department
    [HttpGet("{departmentCode}")]
    public ActionResult<Budget> Get(string departmentCode)
    {
        var department = DepartmentBudgets.Find(departmentCode);
        if (department == null)
            return NotFound(new { message = $"Unknown department code '{departmentCode}'." });

        return Ok(ToBudget(department));
    }

    // Spent = what Emmanuel's ExpenseService has already taken off the budget.
    private Budget ToBudget(Department department)
    {
        decimal remaining = _expenseService.GetRemainingBudget(department.Code) ?? department.Allocated;
        decimal spent = department.Allocated - remaining;

        return new Budget(department.Code, department.Name, department.Allocated, spent, remaining);
    }
}