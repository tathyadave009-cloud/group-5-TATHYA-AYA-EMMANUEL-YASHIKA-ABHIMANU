namespace Finance.Api.Models;

// What GET /api/budgets returns for one department.
public record Budget(
    string DepartmentCode,
    string DepartmentName,
    decimal Allocated,
    decimal Spent,
    decimal Remaining);