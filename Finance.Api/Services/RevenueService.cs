
namespace Finance.Api.Services;

// Represents a recorded city revenue transaction.
public record Revenue(
    Guid Id,
    string Source,
    string DepartmentCode,
    decimal Amount,
    string Description,
    DateTime CreatedAtUtc,
    string? ResidentId = null,
    DateOnly? DueDate = null,
    string? OverdueNoticeStatus = null);

// Represents the result of recording revenue.
public record RevenueResult(
    bool Approved,
    string Message,
    Revenue? Revenue);

public class RevenueService
{
    private readonly List<Revenue> _revenues = new();
    private readonly object _lock = new();

    // Allowed revenue sources.
    private static readonly HashSet<string> ValidSources =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "FINES",
            "FARES",
            "BILLS",
            "FEES"
        };

    // Validates and records a revenue transaction.

    public RevenueResult RecordRevenue(
        string? source,
        string? departmentCode,
        decimal amount,
        string? description,
        string? residentId = null,
        DateOnly? dueDate = null)

    {
        // Validate revenue source.
        if (string.IsNullOrWhiteSpace(source) ||
            !ValidSources.Contains(source.Trim()))
        {
            return new(false, "Invalid revenue source.", null);
        }

        // Validate department code.
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return new(false, "Department code is required.", null);
        }

        // Validate amount.
        if (amount <= 0)
        {
            return new(false, "Amount must be greater than zero.", null);
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return new(
                false,
                "Amount must have at most two decimal places.",
                null);
        }

        // Validate description.
        if (string.IsNullOrWhiteSpace(description))
        {
            return new(false, "Description is required.", null);
        }

        // Verify that the department exists.
        var department = Finance.Api.Data.DepartmentBudgets.Find(
            departmentCode);

        if (department is null)
        {
            return new(false, "Unknown department code.", null);
        }

        // Safely create and store the revenue transaction.
        lock (_lock)
        {

            var revenue = new Revenue(
                Guid.NewGuid(),
                source.Trim().ToUpperInvariant(),
                department.Code,
                amount,
                description.Trim(),
                DateTime.UtcNow,
                source.Trim().Equals("FINES", StringComparison.OrdinalIgnoreCase)
                    ? residentId?.Trim()
                    : null,
                    ? dueDate
                    : null);



            _revenues.Add(revenue);

            return new(
                true,
                "Revenue recorded successfully.",
                revenue);
        }
    }


    // Finds a revenue transaction using its ID.
    public Revenue? GetRevenueById(Guid id)
    {
        lock (_lock)
        {
            return _revenues.Find(revenue => revenue.Id == id);
        }
    }

    // Updates the result of an overdue fine notification.
    public bool UpdateOverdueNoticeStatus(Guid id, string status)
    {
        lock (_lock)
        {
            int index = _revenues.FindIndex(
                revenue => revenue.Id == id);

            if (index < 0)
            {
                return false;
            }

            _revenues[index] = _revenues[index] with
            {
                OverdueNoticeStatus = status
            };

            return true;
        }
    }

    // Returns all recorded revenue transactions.
    public Revenue[] GetRevenues()
    {
        lock (_lock)
        {
            return _revenues.ToArray();
        }
    }

    // Returns total revenue grouped by source.
    public Dictionary<string, decimal> GetRevenueSummary()
    {
        lock (_lock)
        {
            var summary = ValidSources.ToDictionary(
                source => source,
                source => 0m,
                StringComparer.OrdinalIgnoreCase);

            foreach (var revenue in _revenues)
            {
                summary[revenue.Source] += revenue.Amount;
            }

            return summary;
        }
    }
}
