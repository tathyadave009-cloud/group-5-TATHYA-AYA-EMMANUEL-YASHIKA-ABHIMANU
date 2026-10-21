using Finance.Api.Controllers;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

public class ExpenseTests
{
    private static ExpenseService CreateService()
    {
        return new ExpenseService(new Dictionary<string, decimal>
        {
            ["EMS"] = 1000m,
            ["HR"] = 500m
        });
    }

    [Fact]
    public void WithinBudget_RecordsExpenseAndReducesBalance()
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", 250m, "Equipment");

        Assert.True(result.Approved);
        Assert.Equal(750m, service.GetRemainingBudget("EMS"));
        var saved = Assert.Single(service.GetExpenses());
        Assert.Equal(result.Expense, saved);
        Assert.Equal(250m, saved.Amount);
        Assert.Equal("EMS", saved.DepartmentCode);
        Assert.Equal("Equipment", saved.Description);
    }

    [Fact]
    public void ExactBudget_IsApproved()
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", 1000m, "Equipment");

        Assert.True(result.Approved);
        Assert.Equal(0m, service.GetRemainingBudget("EMS"));
        Assert.Single(service.GetExpenses());
    }

    [Fact]
    public void OverBudget_IsRejectedWithoutChangingData()
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", 1000.01m, "Equipment");

        Assert.False(result.Approved);
        Assert.Null(result.Expense);
        Assert.Equal(1000m, result.RemainingBudget);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }

    [Fact]
    public void PreviousExpenses_AreIncludedInBudgetCheck()
    {
        var service = CreateService();

        var first = service.RecordExpense("EMS", 700m, "Equipment");
        var second = service.RecordExpense("EMS", 400m, "Supplies");

        Assert.True(first.Approved);
        Assert.False(second.Approved);
        Assert.Equal(300m, service.GetRemainingBudget("EMS"));
        Assert.Single(service.GetExpenses());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void NonPositiveAmount_IsRejected(int amount)
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", amount, "Supplies");

        Assert.False(result.Approved);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }

    [Fact]
    public void MoreThanTwoDecimalPlaces_IsRejected()
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", 10.001m, "Supplies");

        Assert.False(result.Approved);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("UNKNOWN")]
    public void InvalidDepartment_IsRejected(string? code)
    {
        var service = CreateService();

        var result = service.RecordExpense(code, 100m, "Supplies");

        Assert.False(result.Approved);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingDescription_IsRejected(string? description)
    {
        var service = CreateService();

        var result = service.RecordExpense("EMS", 100m, description);

        Assert.False(result.Approved);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }

    [Fact]
    public void DepartmentCode_IgnoresCaseAndSurroundingSpaces()
    {
        var service = CreateService();

        var result = service.RecordExpense(" ems ", 100m, "Supplies");

        Assert.True(result.Approved);
        Assert.Equal("EMS", result.Expense!.DepartmentCode);
        Assert.Equal(900m, service.GetRemainingBudget("EMS"));
    }

    [Fact]
    public void Expense_DoesNotReduceAnotherDepartmentsBudget()
    {
        var service = CreateService();

        service.RecordExpense("EMS", 100m, "Supplies");

        Assert.Equal(900m, service.GetRemainingBudget("EMS"));
        Assert.Equal(500m, service.GetRemainingBudget("HR"));
    }

    [Fact]
    public void Controller_Returns201ForApprovedExpense()
    {
        var service = CreateService();
        var controller = new ExpensesController(service);

        var response = controller.Post(
            new CreateExpenseRequest("EMS", 100m, "Supplies"));

        var status = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(201, status.StatusCode);
        var body = Assert.IsType<ExpenseResult>(status.Value);
        Assert.True(body.Approved);
        Assert.Equal(900m, body.RemainingBudget);
    }

    [Fact]
    public void Controller_Returns400ForOverBudgetExpense()
    {
        var service = CreateService();
        var controller = new ExpensesController(service);

        var response = controller.Post(
            new CreateExpenseRequest("EMS", 1001m, "Supplies"));

        var status = Assert.IsType<BadRequestObjectResult>(response.Result);
        var body = Assert.IsType<ExpenseResult>(status.Value);
        Assert.False(body.Approved);
        Assert.Equal(1000m, service.GetRemainingBudget("EMS"));
        Assert.Empty(service.GetExpenses());
    }
}