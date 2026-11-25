using Finance.Api.Data;

namespace Finance.Api.Services;

/// <summary>
/// BF-24: a financial report for a date range.
/// </summary>
/// <param name="From">First day of the report (inclusive).</param>
/// <param name="To">Last day of the report (inclusive).</param>
/// <param name="SpentByDepartment">Total expenses per department code (all 7 departments, 0 if none).</param>
/// <param name="RevenueBySource">Total revenue per source: FINES, FARES, BILLS, FEES (0 if none).</param>
/// <param name="TotalSpent">All expenses in the range.</param>
/// <param name="TotalRevenue">All revenue in the range.</param>
/// <param name="Net">TotalRevenue minus TotalSpent.</param>
/// <param name="Currency">Always CAD.</param>
public record ReportSummary(
    DateTime From,
    DateTime To,
    Dictionary<string, decimal> SpentByDepartment,
    Dictionary<string, decimal> RevenueBySource,
    decimal TotalSpent,
    decimal TotalRevenue,
    decimal Net,
    string Currency);

/// <summary>
/// BF-24: builds the financial report from the expenses and revenue
/// that the other services already keep.
/// </summary>
public class ReportService
{
    /// <summary>The revenue sources that always appear in the report.</summary>
    public static readonly string[] Sources = { "FINES", "FARES", "BILLS", "FEES" };

    private readonly ExpenseService _expenseService;
    private readonly RevenueService _revenueService;

    public ReportService(ExpenseService expenseService, RevenueService revenueService)
    {
        _expenseService = expenseService;
        _revenueService = revenueService;
    }

    /// <summary>
    /// Totals for every expense and revenue record created between
    /// <paramref name="from"/> and <paramref name="to"/>, both days included (UTC).
    /// </summary>
    public ReportSummary Build(DateTime from, DateTime to)
    {
        DateTime start = from.Date;
        DateTime endExclusive = to.Date.AddDays(1);

        bool InRange(DateTime createdAtUtc) =>
            createdAtUtc >= start && createdAtUtc < endExclusive;

        var spent = DepartmentBudgets.All.ToDictionary(d => d.Code, _ => 0m);
        foreach (var expense in _expenseService.GetExpenses().Where(e => InRange(e.CreatedAtUtc)))
        {
            string code = expense.DepartmentCode.ToUpperInvariant();
            spent[code] = spent.GetValueOrDefault(code) + expense.Amount;
        }

        var revenue = Sources.ToDictionary(s => s, _ => 0m);
        foreach (var item in _revenueService.GetRevenues().Where(r => InRange(r.CreatedAtUtc)))
        {
            string source = item.Source.ToUpperInvariant();
            revenue[source] = revenue.GetValueOrDefault(source) + item.Amount;
        }

        decimal totalSpent = spent.Values.Sum();
        decimal totalRevenue = revenue.Values.Sum();

        return new ReportSummary(
            start,
            to.Date,
            spent,
            revenue,
            totalSpent,
            totalRevenue,
            totalRevenue - totalSpent,
            "CAD");
    }
}
