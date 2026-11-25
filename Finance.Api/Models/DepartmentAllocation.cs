namespace Finance.Api.Models;

public record DepartmentAllocation(
    string DepartmentCode,
    string DepartmentName,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    string Currency = "CAD");
