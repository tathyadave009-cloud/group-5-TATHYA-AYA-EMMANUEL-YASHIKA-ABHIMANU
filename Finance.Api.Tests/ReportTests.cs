using Finance.Api.Controllers;
using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Tests;

// BF-24: financial report for a date range.
public class ReportTests
{
    private static (ReportsController controller, ExpenseService expenses, RevenueService revenue) Create()
    {
        var expenses = new ExpenseService(new TestDbFactory(),DepartmentBudgets.StartingBudgets());
        var revenue = new RevenueService();
        return (new ReportsController(expenses, revenue), expenses, revenue);
    }

    private static DateTime Today => DateTime.UtcNow.Date;

    [Fact]
    public void Summary_TotalsMatchExpensesAndRevenueInRange()
    {
        var (controller, expenses, revenue) = Create();
        expenses.RecordExpense("EMS", 1000m, "Fuel");
        expenses.RecordExpense("EMS", 500m, "Supplies");
        expenses.RecordExpense("HR", 2000m, "Salaries");
        revenue.RecordRevenue("FINES", "SEC", 150m, "Parking fine");
        revenue.RecordRevenue("FARES", "TRANSIT", 3.25m, "Bus fare");

        var ok = Assert.IsType<OkObjectResult>(
            controller.GetSummary(Today.AddDays(-1), Today.AddDays(1)).Result);
        var report = Assert.IsType<ReportSummary>(ok.Value);

        Assert.Equal(1500m, report.SpentByDepartment["EMS"]);
        Assert.Equal(2000m, report.SpentByDepartment["HR"]);
        Assert.Equal(0m, report.SpentByDepartment["FIN"]);
        Assert.Equal(150m, report.RevenueBySource["FINES"]);
        Assert.Equal(3.25m, report.RevenueBySource["FARES"]);
        Assert.Equal(3500m, report.TotalSpent);
        Assert.Equal(153.25m, report.TotalRevenue);
        Assert.Equal(153.25m - 3500m, report.Net);
        Assert.Equal("CAD", report.Currency);
    }

    [Fact]
    public void Summary_AllDepartmentsAndSourcesAlwaysListed()
    {
        var (controller, _, _) = Create();

        var ok = Assert.IsType<OkObjectResult>(controller.GetSummary(Today, Today).Result);
        var report = Assert.IsType<ReportSummary>(ok.Value);

        Assert.Equal(7, report.SpentByDepartment.Count);
        Assert.Equal(new[] { "FINES", "FARES", "BILLS", "FEES" }, report.RevenueBySource.Keys);
        Assert.Equal(0m, report.Net);
    }

    [Fact]
    public void Summary_RecordsOutsideTheRangeAreLeftOut()
    {
        var (controller, expenses, revenue) = Create();
        expenses.RecordExpense("EMS", 1000m, "Fuel");
        revenue.RecordRevenue("FINES", "SEC", 150m, "Parking fine");

        var ok = Assert.IsType<OkObjectResult>(
            controller.GetSummary(Today.AddDays(-30), Today.AddDays(-1)).Result);
        var report = Assert.IsType<ReportSummary>(ok.Value);

        Assert.Equal(0m, report.TotalSpent);
        Assert.Equal(0m, report.TotalRevenue);
    }

    [Fact]
    public void Summary_OneDayRangeIncludesThatWholeDay()
    {
        var (controller, expenses, _) = Create();
        expenses.RecordExpense("EMS", 1000m, "Fuel");

        var ok = Assert.IsType<OkObjectResult>(controller.GetSummary(Today, Today).Result);

        Assert.Equal(1000m, Assert.IsType<ReportSummary>(ok.Value).TotalSpent);
    }

    [Fact]
    public void Summary_FromAfterTo_Returns400InClassFormat()
    {
        var (controller, _, _) = Create();

        var bad = Assert.IsType<BadRequestObjectResult>(
            controller.GetSummary(Today, Today.AddDays(-1)).Result);
        var error = Assert.IsType<ApiError>(bad.Value);

        Assert.Equal("bad_request", error.Error);
    }

    [Fact]
    public void Summary_MissingDate_Returns400()
    {
        var (controller, _, _) = Create();

        Assert.IsType<BadRequestObjectResult>(controller.GetSummary(null, Today).Result);
        Assert.IsType<BadRequestObjectResult>(controller.GetSummary(Today, null).Result);
    }
}
