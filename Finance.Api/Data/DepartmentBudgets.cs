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

    private static readonly Dictionary<string, string> NameAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transit"] = "TRANSIT",
        ["public transport"] = "TRANSIT",
        ["publictransport"] = "TRANSIT",
        ["transport"] = "TRANSIT",
        ["util"] = "UTIL",
        ["utilities"] = "UTIL",
        ["utility"] = "UTIL",
        ["res"] = "RES",
        ["residence"] = "RES",
        ["registration"] = "RES",
        ["residence registration"] = "RES",
        ["residenceregistration"] = "RES",
        ["sec"] = "SEC",
        ["security"] = "SEC",
        ["fin"] = "FIN",
        ["finance"] = "FIN",
        ["budget"] = "FIN",
        ["budget & finance"] = "FIN",
        ["budget and finance"] = "FIN",
        ["budgetfinance"] = "FIN",
        ["hr"] = "HR",
        ["staff"] = "HR",
        ["staff / hr"] = "HR",
        ["staff/hr"] = "HR",
        ["staffhr"] = "HR",
        ["human resources"] = "HR",
        ["humanresources"] = "HR",
        ["ems"] = "EMS",
        ["emergency"] = "EMS",
        ["emergency services"] = "EMS",
        ["emergencyservices"] = "EMS"
    };

    // Finds a department by code, ignoring upper/lower case and spaces.
    public static Department? Find(string? code) =>
        All.FirstOrDefault(d => d.Code.Equals(code?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Accepts a code or a name (e.g. emergency = EMS, transit = TRANSIT, hr = HR). Unknown = null.
    public static Department? FindByCodeOrName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string trimmed = input.Trim();

        // 1. Direct code lookup (e.g., "EMS", "TRANSIT")
        var department = Find(trimmed);
        if (department != null)
        {
            return department;
        }

        // 2. Direct name match (e.g., "Emergency Services", "Public Transport")
        department = All.FirstOrDefault(d => d.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (department != null)
        {
            return department;
        }

        // 3. Known alias mapping (e.g., "emergency", "transit", "hr")
        if (NameAliasMap.TryGetValue(trimmed, out string? mappedCode))
        {
            return Find(mappedCode);
        }

        // 4. Normalized string (letters and digits only)
        string alphanumeric = new string(trimmed.Where(char.IsLetterOrDigit).ToArray());
        if (NameAliasMap.TryGetValue(alphanumeric, out mappedCode))
        {
            return Find(mappedCode);
        }

        return null;
    }

    // Starting budgets in the format Emmanuel's ExpenseService expects.
    public static Dictionary<string, decimal> StartingBudgets() =>
        All.ToDictionary(d => d.Code, d => d.Allocated);
}
