using Finance.Api.Data;
using Microsoft.EntityFrameworkCore;

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

// BF-06: the answer when Finance tries to change a department's budget.
public record BudgetChangeResult(
    bool Success,
    string Message);

public record ExpensePage(
    Expense[] Items,
    int Page,
    int PageSize,
    int TotalCount);

public class ExpenseService : IBudgetFundingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly object _lock = new();

    public ExpenseService(
        IDbContextFactory<AppDbContext> dbFactory,
        Dictionary<string, decimal> budgets)
    {
        _dbFactory = dbFactory;

        using var db = _dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        // Seed only departments that are not in the database yet,
        // so budget changes survive a restart.
        foreach (var (code, amount) in budgets)
        {
            string key = code.Trim().ToUpperInvariant();
            if (!db.Budgets.Any(b => b.DepartmentCode == key))
            {
                db.Budgets.Add(new BudgetEntity
                {
                    DepartmentCode = key,
                    Allocated = amount
                });
            }
        }

        db.SaveChanges();
    }

    private static decimal SpentFor(AppDbContext db, string code) =>
        db.Expenses
            .Where(e => e.DepartmentCode == code)
            .Sum(e => (decimal?)e.Amount) ?? 0m;

    private static Expense ToExpense(ExpenseEntity e) =>
        new(e.Id, e.DepartmentCode, e.Amount, e.Description, e.CreatedAtUtc);

    public ExpenseResult RecordExpense(
        string? departmentCode,
        decimal amount,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
            return new(false, "Department code is required.", null, null);

        if (amount <= 0)
            return new(false, "Amount must be greater than zero.", null, null);

        if (decimal.Round(amount, 2) != amount)
            return new(false, "Amount must have at most two decimal places.", null, null);

        if (string.IsNullOrWhiteSpace(description))
            return new(false, "Description is required.", null, null);

        string code = departmentCode.Trim().ToUpperInvariant();

        // Check and save together to prevent simultaneous overspending.
        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            var budget = db.Budgets.Find(code);
            if (budget == null)
                return new(false, "Unknown department code.", null, null);

            decimal remaining = budget.Allocated - SpentFor(db, code);

            if (amount > remaining)
                return new(false, "Expense exceeds the remaining budget.", remaining, null);

            var expense = new Expense(
                Guid.NewGuid(),
                code,
                amount,
                description.Trim(),
                DateTime.UtcNow);

            db.Expenses.Add(new ExpenseEntity
            {
                Id = expense.Id,
                DepartmentCode = expense.DepartmentCode,
                Amount = expense.Amount,
                Description = expense.Description,
                CreatedAtUtc = expense.CreatedAtUtc
            });
            db.SaveChanges();

            return new(true, "Expense approved.", remaining - amount, expense);
        }
    }

    public decimal? GetRemainingBudget(string departmentCode)
    {
        string code = departmentCode.Trim().ToUpperInvariant();

        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            var budget = db.Budgets.Find(code);
            if (budget == null)
                return null;

            return budget.Allocated - SpentFor(db, code);
        }
    }

    // BF-06: the department's current yearly budget, or null if unknown.
    public decimal? GetAllocated(string departmentCode)
    {
        string code = departmentCode.Trim().ToUpperInvariant();

        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            var budget = db.Budgets.Find(code);
            return budget?.Allocated;
        }
    }

    // BF-06: Finance changes a department's yearly budget.
    // Refused if the new budget is lower than what is already spent.
    public BudgetChangeResult SetAllocation(
        string? departmentCode,
        decimal newAllocated)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
            return new(false, "Department code is required.");

        if (newAllocated <= 0)
            return new(false, "New budget must be greater than zero.");

        if (decimal.Round(newAllocated, 2) != newAllocated)
            return new(false, "New budget must have at most two decimal places.");

        string code = departmentCode.Trim().ToUpperInvariant();

        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            var budget = db.Budgets.Find(code);
            if (budget == null)
                return new(false, "Unknown department code.");

            decimal spent = SpentFor(db, code);

            if (newAllocated < spent)
            {
                return new(
                    false,
                    $"New budget is lower than what is already spent ({spent:N2}).");
            }

            budget.Allocated = newAllocated;
            db.SaveChanges();

            return new(true, "Budget updated.");
        }
    }

    // Used by BF-07 when a funding request is approved.
    // Adds the money to the yearly budget, so what is left goes up too.
    public void AddFunds(string departmentCode, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
            throw new ArgumentException("Department code is required.");

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amount must have at most two decimal places.");

        string code = departmentCode.Trim().ToUpperInvariant();

        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            var budget = db.Budgets.Find(code);
            if (budget == null)
                throw new ArgumentException("Unknown department code.");

            budget.Allocated += amount;
            db.SaveChanges();
        }
    }

    // Retained for compatibility with the existing Sprint 1 tests.
    public Expense[] GetExpenses()
    {
        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            return db.Expenses
                .AsNoTracking()
                .OrderBy(e => e.Seq)
                .AsEnumerable()
                .Select(ToExpense)
                .ToArray();
        }
    }

    public ExpensePage GetExpenses(
        string? departmentCode,
        int page,
        int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");

        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Page size must be between 1 and 100.");

        lock (_lock)
        {
            using var db = _dbFactory.CreateDbContext();

            IQueryable<ExpenseEntity> query = db.Expenses.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(departmentCode))
            {
                string code = departmentCode.Trim().ToUpperInvariant();

                if (!db.Budgets.Any(b => b.DepartmentCode == code))
                {
                    throw new ArgumentException(
                        "Unknown department code.",
                        nameof(departmentCode));
                }

                query = query.Where(e => e.DepartmentCode == code);
            }

            int totalCount = query.Count();

            Expense[] items = query
                .OrderByDescending(e => e.Seq)   // newest first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsEnumerable()
                .Select(ToExpense)
                .ToArray();

            return new ExpensePage(items, page, pageSize, totalCount);
        }
    }
}