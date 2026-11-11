using Finance.Api.Controllers;
using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

public class BudgetTests
{
    private static BudgetsController CreateController(out ExpenseService service)
    {
        service = new ExpenseService(DepartmentBudgets.StartingBudgets());
        return new BudgetsController(service);
    }

    [Fact]
    public void GetAll_ReturnsAllSevenDepartments()
    {
        var controller = CreateController(out _);

        var ok = Assert.IsType<OkObjectResult>(controller.GetAll().Result);
        var budgets = Assert.IsAssignableFrom<IEnumerable<Budget>>(ok.Value);

        Assert.Equal(7, budgets.Count());
        Assert.All(budgets, b => Assert.Equal(b.Allocated, b.Remaining));
    }

    [Fact]
    public void Get_KnownDepartment_ReturnsItsBudget()
    {
        var controller = CreateController(out _);

        var ok = Assert.IsType<OkObjectResult>(controller.Get("ems").Result);
        var budget = Assert.IsType<Budget>(ok.Value);

        Assert.Equal("EMS", budget.DepartmentCode);
        Assert.Equal(4_500_000m, budget.Allocated);
    }

    [Fact]
    public void Get_UnknownDepartment_Returns404()
    {
        var controller = CreateController(out _);

        Assert.IsType<NotFoundObjectResult>(controller.Get("PARKS").Result);
    }

    [Fact]
    public void Budget_ShowsMoneySpentByExpenses()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 45_000m, "Defibrillators");

        var ok = Assert.IsType<OkObjectResult>(controller.Get("EMS").Result);
        var budget = Assert.IsType<Budget>(ok.Value);

        Assert.Equal(45_000m, budget.Spent);
        Assert.Equal(4_455_000m, budget.Remaining);
    }

    // ---------- BF-06: change a department's budget ----------

    [Fact]
    public void Update_RaisesBudget()
    {
        var controller = CreateController(out _);

        var ok = Assert.IsType<OkObjectResult>(
            controller.Update("EMS", new UpdateBudgetRequest(5_000_000m)).Result);
        var budget = Assert.IsType<Budget>(ok.Value);

        Assert.Equal(5_000_000m, budget.Allocated);
        Assert.Equal(0m, budget.Spent);
        Assert.Equal(5_000_000m, budget.Remaining);
    }

    [Fact]
    public void Update_LowerButAboveSpent_KeepsSpent()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 100_000m, "Fuel");

        var ok = Assert.IsType<OkObjectResult>(
            controller.Update("EMS", new UpdateBudgetRequest(1_000_000m)).Result);
        var budget = Assert.IsType<Budget>(ok.Value);

        Assert.Equal(1_000_000m, budget.Allocated);
        Assert.Equal(100_000m, budget.Spent);
        Assert.Equal(900_000m, budget.Remaining);
    }

    [Fact]
    public void Update_BelowSpent_Returns400AndBudgetUnchanged()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 100_000m, "Fuel");

        Assert.IsType<BadRequestObjectResult>(
            controller.Update("EMS", new UpdateBudgetRequest(50_000m)).Result);

        Assert.Equal(4_500_000m, service.GetAllocated("EMS"));
        Assert.Equal(4_400_000m, service.GetRemainingBudget("EMS"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("100.555")]
    public void Update_InvalidAmount_Returns400(string amountText)
    {
        var controller = CreateController(out _);
        decimal amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsType<BadRequestObjectResult>(
            controller.Update("EMS", new UpdateBudgetRequest(amount)).Result);
    }

    [Fact]
    public void Update_UnknownDepartment_Returns404()
    {
        var controller = CreateController(out _);

        Assert.IsType<NotFoundObjectResult>(
            controller.Update("PARKS", new UpdateBudgetRequest(1_000m)).Result);
    }

    [Fact]
    public void AddFunds_RaisesAllocatedAndRemaining_SpentStaysTheSame()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 45_000m, "Defibrillators");

        service.AddFunds("EMS", 250_000m);

        var ok = Assert.IsType<OkObjectResult>(controller.Get("EMS").Result);
        var budget = Assert.IsType<Budget>(ok.Value);

        Assert.Equal(4_750_000m, budget.Allocated);
        Assert.Equal(45_000m, budget.Spent);
        Assert.Equal(4_705_000m, budget.Remaining);
    }

    // ---------- BF-13: city summary + low-budget warning ----------

    [Fact]
    public void Summary_TotalsMatchAllDepartments()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 45_000m, "Defibrillators");

        var ok = Assert.IsType<OkObjectResult>(controller.GetSummary().Result);
        var summary = Assert.IsType<BudgetSummary>(ok.Value);

        Assert.Equal(19_600_000m, summary.TotalAllocated);
        Assert.Equal(45_000m, summary.TotalSpent);
        Assert.Equal(19_555_000m, summary.TotalRemaining);
        Assert.Equal("CAD", summary.Currency);
        Assert.Empty(summary.LowBudgetDepartments);
    }

    [Fact]
    public void LowBudget_FlaggedWhenLessThanTenPercentLeft()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("FIN", 550_000m, "Audit software");   // 50,000 of 600,000 left

        var ok = Assert.IsType<OkObjectResult>(controller.Get("FIN").Result);
        Assert.True(Assert.IsType<Budget>(ok.Value).LowBudget);

        var summaryOk = Assert.IsType<OkObjectResult>(controller.GetSummary().Result);
        var summary = Assert.IsType<BudgetSummary>(summaryOk.Value);
        Assert.Equal(new[] { "FIN" }, summary.LowBudgetDepartments);
    }
}
