using Finance.Api.Controllers;
using Finance.Api.Data;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

public class AllocationTests
{
    private static AllocationsController CreateController(out ExpenseService service)
    {
        service = new ExpenseService(DepartmentBudgets.StartingBudgets());
        return new AllocationsController(service);
    }

    [Theory]
    [InlineData("emergency", "EMS", "Emergency Services")]
    [InlineData("EMERGENCY", "EMS", "Emergency Services")]
    [InlineData("Emergency Services", "EMS", "Emergency Services")]
    [InlineData("ems", "EMS", "Emergency Services")]
    [InlineData("transit", "TRANSIT", "Public Transport")]
    [InlineData("Public Transport", "TRANSIT", "Public Transport")]
    [InlineData("hr", "HR", "Staff / HR")]
    [InlineData("Staff / HR", "HR", "Staff / HR")]
    [InlineData("util", "UTIL", "Utilities")]
    [InlineData("res", "RES", "Residence Registration")]
    [InlineData("sec", "SEC", "Security")]
    [InlineData("fin", "FIN", "Budget & Finance")]
    public void FindByCodeOrName_KnownDepartmentOrAlias_ReturnsDepartment(
        string input,
        string expectedCode,
        string expectedName)
    {
        var department = DepartmentBudgets.FindByCodeOrName(input);

        Assert.NotNull(department);
        Assert.Equal(expectedCode, department.Code);
        Assert.Equal(expectedName, department.Name);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("xyz")]
    [InlineData("parks")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FindByCodeOrName_UnknownOrEmpty_ReturnsNull(string? input)
    {
        var department = DepartmentBudgets.FindByCodeOrName(input);
        Assert.Null(department);
    }

    [Fact]
    public void Controller_GetByName_Emergency_ReturnsEMSAllocation()
    {
        var controller = CreateController(out _);

        var response = controller.Get("emergency");
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var allocation = Assert.IsType<DepartmentAllocation>(ok.Value);

        Assert.Equal("EMS", allocation.DepartmentCode);
        Assert.Equal("Emergency Services", allocation.DepartmentName);
        Assert.Equal(4_500_000m, allocation.Allocated);
        Assert.Equal(0m, allocation.Spent);
        Assert.Equal(4_500_000m, allocation.Remaining);
        Assert.Equal("CAD", allocation.Currency);
    }

    [Fact]
    public void Controller_GetByCode_Transit_ReturnsTransitAllocation()
    {
        var controller = CreateController(out _);

        var response = controller.Get("TRANSIT");
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var allocation = Assert.IsType<DepartmentAllocation>(ok.Value);

        Assert.Equal("TRANSIT", allocation.DepartmentCode);
        Assert.Equal("Public Transport", allocation.DepartmentName);
        Assert.Equal(4_000_000m, allocation.Allocated);
        Assert.Equal("CAD", allocation.Currency);
    }

    [Fact]
    public void Controller_GetUnknownDepartment_Returns404WithApiError()
    {
        var controller = CreateController(out _);

        var response = controller.Get("xyz");
        var notFound = Assert.IsType<NotFoundObjectResult>(response.Result);
        var error = Assert.IsType<ApiError>(notFound.Value);

        Assert.Equal("not_found", error.Error);
        Assert.Contains("xyz", error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    [Fact]
    public void Controller_AfterExpense_CalculatesSpentAndRemainingCorrectly()
    {
        var controller = CreateController(out var service);
        service.RecordExpense("EMS", 50_000m, "Medical supplies");

        var response = controller.Get("emergency");
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var allocation = Assert.IsType<DepartmentAllocation>(ok.Value);

        Assert.Equal(50_000m, allocation.Spent);
        Assert.Equal(4_450_000m, allocation.Remaining);
    }

    [Fact]
    public void Controller_GetAll_ReturnsAllSevenDepartments()
    {
        var controller = CreateController(out _);

        var response = controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var allocations = Assert.IsAssignableFrom<IEnumerable<DepartmentAllocation>>(ok.Value);

        Assert.Equal(7, allocations.Count());
    }
}
