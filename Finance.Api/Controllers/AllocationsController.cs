using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/budget/v1/allocations")]
[Route("allocations")]
public class AllocationsController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public AllocationsController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<DepartmentAllocation>> GetAll()
    {
        var allocations = DepartmentBudgets.All.Select(ToAllocation).ToList();
        return Ok(allocations);
    }

    [HttpGet("{dept}")]
    public ActionResult<DepartmentAllocation> Get(string dept)
    {
        var department = DepartmentBudgets.FindByCodeOrName(dept);
        if (department == null)
        {
            return NotFound(new ApiError(
                "not_found",
                $"Unknown department '{dept}'.",
                HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("n")));
        }

        return Ok(ToAllocation(department));
    }

    private DepartmentAllocation ToAllocation(Department department)
    {
        decimal remaining = _expenseService.GetRemainingBudget(department.Code) ?? department.Allocated;
        decimal spent = department.Allocated - remaining;

        return new DepartmentAllocation(
            department.Code,
            department.Name,
            department.Allocated,
            spent,
            remaining,
            "CAD");
    }
}
