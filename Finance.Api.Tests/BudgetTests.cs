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
}