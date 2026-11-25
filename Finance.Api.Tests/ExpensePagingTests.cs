using Finance.Api.Controllers;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

public class ExpensePagingTests
{
    private static ExpenseService CreateService()
    {
        return new ExpenseService(
            new Dictionary<string, decimal>
            {
                ["EMS"] = 1000m,
                ["HR"] = 1000m
            });
    }

    [Fact]
    public void GetExpenses_FiltersByDepartmentAndShowsNewestFirst()
    {
        ExpenseService service = CreateService();

        service.RecordExpense("EMS", 100m, "Old EMS expense");
        service.RecordExpense("HR", 50m, "HR expense");
        service.RecordExpense("EMS", 200m, "New EMS expense");

        ExpensePage result = service.GetExpenses(
            "EMS",
            page: 1,
            pageSize: 25);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.All(
            result.Items,
            expense => Assert.Equal("EMS", expense.DepartmentCode));

        Assert.Equal(
            "New EMS expense",
            result.Items[0].Description);

        Assert.Equal(
            "Old EMS expense",
            result.Items[1].Description);
    }

    [Fact]
    public void GetExpenses_ReturnsTheRequestedPage()
    {
        ExpenseService service = CreateService();

        service.RecordExpense("EMS", 10m, "First");
        service.RecordExpense("EMS", 20m, "Second");
        service.RecordExpense("EMS", 30m, "Third");

        ExpensePage result = service.GetExpenses(
            null,
            page: 2,
            pageSize: 2);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);

        Expense item = Assert.Single(result.Items);
        Assert.Equal("First", item.Description);
    }

    [Fact]
    public void Controller_RejectsPageNumberBelowOne()
    {
        ExpenseService service = CreateService();
        var controller = new ExpensesController(service);

        ActionResult<ExpensePage> response = controller.Get(
            department: null,
            page: 0,
            pageSize: 25);

        BadRequestObjectResult badRequest =
            Assert.IsType<BadRequestObjectResult>(response.Result);

        Assert.Equal(400, badRequest.StatusCode);
    }
}