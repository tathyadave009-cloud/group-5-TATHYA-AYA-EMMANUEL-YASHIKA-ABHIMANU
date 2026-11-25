using Finance.Api.Data;
using Finance.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Finance.Api.Tests;

// Gives each test its own empty in-memory database.
// Reusing one factory for two services simulates an app restart.
public class TestDbFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public AppDbContext CreateDbContext() => new(_options);
}

public class ExpenseServiceDatabaseTests
{
    private static Dictionary<string, decimal> Budgets() => new()
    {
        ["EMS"] = 1000m,
        ["HR"] = 500m
    };

    private static ExpenseService NewService(TestDbFactory? factory = null) =>
        new(factory ?? new TestDbFactory(), Budgets());

    [Fact]
    public void Expense_within_budget_is_approved_and_lowers_remaining()
    {
        var service = NewService();

        var result = service.RecordExpense("EMS", 250m, "Defibrillators");

        Assert.True(result.Approved);
        Assert.Equal(750m, result.RemainingBudget);
        Assert.Equal(750m, service.GetRemainingBudget("EMS"));
    }

    [Fact]
    public void Expense_over_budget_is_rejected_and_changes_nothing()
    {
        var service = NewService();

        var result = service.RecordExpense("HR", 600m, "Too much");

        Assert.False(result.Approved);
        Assert.Equal(500m, service.GetRemainingBudget("HR"));
        Assert.Empty(service.GetExpenses());
    }

    [Fact]
    public void Zero_amount_is_rejected()
    {
        var service = NewService();

        var result = service.RecordExpense("EMS", 0m, "Nothing");

        Assert.False(result.Approved);
    }

    [Fact]
    public void Unknown_department_is_rejected()
    {
        var service = NewService();

        var result = service.RecordExpense("PARKS", 10m, "Benches");

        Assert.False(result.Approved);
        Assert.Equal("Unknown department code.", result.Message);
    }

    [Fact]
    public void Expenses_are_saved_and_survive_a_new_service_instance()
    {
        var factory = new TestDbFactory();
        var first = NewService(factory);
        first.RecordExpense("EMS", 250m, "Defibrillators");

        var restarted = NewService(factory);

        Assert.Single(restarted.GetExpenses());
        Assert.Equal(750m, restarted.GetRemainingBudget("EMS"));
    }

    [Fact]
    public void SetAllocation_changes_the_budget_and_keeps_the_spent_amount()
    {
        var service = NewService();
        service.RecordExpense("EMS", 250m, "Defibrillators");

        var result = service.SetAllocation("EMS", 2000m);

        Assert.True(result.Success);
        Assert.Equal(2000m, service.GetAllocated("EMS"));
        Assert.Equal(1750m, service.GetRemainingBudget("EMS"));
    }

    [Fact]
    public void SetAllocation_below_what_is_spent_is_refused()
    {
        var service = NewService();
        service.RecordExpense("EMS", 250m, "Defibrillators");

        var result = service.SetAllocation("EMS", 100m);

        Assert.False(result.Success);
        Assert.Equal(1000m, service.GetAllocated("EMS"));
    }

    [Fact]
    public void AddFunds_raises_the_budget_and_what_is_left()
    {
        var service = NewService();

        service.AddFunds("EMS", 500m);

        Assert.Equal(1500m, service.GetAllocated("EMS"));
        Assert.Equal(1500m, service.GetRemainingBudget("EMS"));
    }

    [Fact]
    public void Budget_changes_survive_a_restart()
    {
        var factory = new TestDbFactory();
        var first = NewService(factory);
        first.SetAllocation("EMS", 2000m);

        // The restarted service starts with the original budgets again,
        // but the saved budget must not be overwritten.
        var restarted = NewService(factory);

        Assert.Equal(2000m, restarted.GetAllocated("EMS"));
    }

    [Fact]
    public void Paged_expenses_return_the_newest_first()
    {
        var service = NewService();
        service.RecordExpense("EMS", 10m, "first");
        service.RecordExpense("EMS", 20m, "second");
        service.RecordExpense("EMS", 30m, "third");

        var page = service.GetExpenses(null, 1, 2);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Length);
        Assert.Equal("third", page.Items[0].Description);
        Assert.Equal("second", page.Items[1].Description);
    }
}