namespace Finance.Api.Data;

public record Department(string Code, string Name, decimal Allocated);

// The 7 city departments and their yearly budgets (shared codes agreed with the class).
public static class DepartmentBudgets
{
    public static readonly IReadOnlyList<Department> All = new List<Department>
    {
        new("TRANSIT", "Public Transport", 4_000_000m),
        new("UTIL", "Utilities", 3_500_000m),
        new("RES", "Residence Registration", 800_000m),
        new("SEC", "Security", 5_000_000m),
        new("FIN", "Budget & Finance", 600_000m),
        new("HR", "Staff / HR", 1_200_000m),
        new("EMS", "Emergency Services", 4_500_000m),
    };

    // Finds a department by code, ignoring upper/lower case and spaces.
    public static Department? Find(string? code) =>
        All.FirstOrDefault(d => d.Code.Equals(code?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Starting budgets in the format Emmanuel's ExpenseService expects.
    public static Dictionary<string, decimal> StartingBudgets() =>
        All.ToDictionary(d => d.Code, d => d.Allocated);
}