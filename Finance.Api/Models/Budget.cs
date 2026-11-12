namespace Finance.Api.Models;

// What GET /api/budgets returns for one department.
// LowBudget (BF-13) is true when less than 10% of the budget is left.
public record Budget(
    string DepartmentCode,
    string DepartmentName,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    bool LowBudget);

// BF-06: the body of PUT /api/budgets/{departmentCode}.
public record UpdateBudgetRequest(decimal Allocated);

// BF-13: totals for the whole city.
public record BudgetSummary(
    decimal TotalAllocated,
    decimal TotalSpent,
    decimal TotalRemaining,
    string Currency,
    IReadOnlyList<string> LowBudgetDepartments);
