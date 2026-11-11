namespace Finance.Api.Services;

public record Expense(
    Guid Id,
    string DepartmentCode,
    decimal Amount,
    string Description,
    DateTime CreatedAtUtc);

public record ExpenseResult(
    bool Approved,
    string Message,
    decimal? RemainingBudget,
    Expense? Expense);

public record ExpensePage(
    Expense[] Items,
    int Page,
    int PageSize,
    int TotalCount);

public class ExpenseService
{
    private readonly Dictionary<string, decimal> _remainingBudgets;
    private readonly List<Expense> _expenses = new();
    private readonly object _lock = new();

    public ExpenseService(Dictionary<string, decimal> budgets)
    {
        // Copy the supplied budgets to avoid changing the original data.
        _remainingBudgets = new Dictionary<string, decimal>(
            budgets,
            StringComparer.OrdinalIgnoreCase);
    }

    public ExpenseResult RecordExpense(
        string? departmentCode,
        decimal amount,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return new(
                false,
                "Department code is required.",
                null,
                null);
        }

        if (amount <= 0)
        {
            return new(
                false,
                "Amount must be greater than zero.",
                null,
                null);
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return new(
                false,
                "Amount must have at most two decimal places.",
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return new(
                false,
                "Description is required.",
                null,
                null);
        }

        string code = departmentCode.Trim().ToUpperInvariant();

        // Check and update together to prevent simultaneous overspending.
        lock (_lock)
        {
            if (!_remainingBudgets.TryGetValue(code, out decimal remaining))
            {
                return new(
                    false,
                    "Unknown department code.",
                    null,
                    null);
            }

            if (amount > remaining)
            {
                return new(
                    false,
                    "Expense exceeds the remaining budget.",
                    remaining,
                    null);
            }

            var expense = new Expense(
                Guid.NewGuid(),
                code,
                amount,
                description.Trim(),
                DateTime.UtcNow);

            _expenses.Add(expense);
            _remainingBudgets[code] = remaining - amount;

            return new(
                true,
                "Expense approved.",
                _remainingBudgets[code],
                expense);
        }
    }

    public decimal? GetRemainingBudget(string departmentCode)
    {
        lock (_lock)
        {
            return _remainingBudgets.TryGetValue(
                departmentCode.Trim(),
                out decimal remaining)
                ? remaining
                : null;
        }
    }

    // Retained for compatibility with the existing Sprint 1 tests.
    public Expense[] GetExpenses()
    {
        lock (_lock)
        {
            return _expenses.ToArray();
        }
    }

    public ExpensePage GetExpenses(
        string? departmentCode,
        int page,
        int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be at least 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Page size must be between 1 and 100.");
        }

        lock (_lock)
        {
            IEnumerable<Expense> query = _expenses
                .AsEnumerable()
                .Reverse();

            if (!string.IsNullOrWhiteSpace(departmentCode))
            {
                string code = departmentCode
                    .Trim()
                    .ToUpperInvariant();

                if (!_remainingBudgets.ContainsKey(code))
                {
                    throw new ArgumentException(
                        "Unknown department code.",
                        nameof(departmentCode));
                }

                query = query.Where(
                    expense => expense.DepartmentCode.Equals(
                        code,
                        StringComparison.OrdinalIgnoreCase));
            }

            Expense[] matchingExpenses = query.ToArray();
            int totalCount = matchingExpenses.Length;

            Expense[] items = matchingExpenses
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return new ExpensePage(
                items,
                page,
                pageSize,
                totalCount);
        }
    }
}