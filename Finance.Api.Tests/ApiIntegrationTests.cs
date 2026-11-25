using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Finance.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Finance.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_HealthEndpoint_Returns200WithVersion020()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("BudgetFinance", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("0.2.0", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task GetHealth_ApiHealthEndpoint_Returns200WithVersion020()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("BudgetFinance", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("0.2.0", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task GetBudgets_NewRoute_ReturnsAllDepartments()
    {
        var response = await _client.GetAsync("/api/budget/v1/budgets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var budgets = await response.Content.ReadFromJsonAsync<List<Budget>>();
        Assert.NotNull(budgets);
        Assert.Equal(7, budgets.Count);
    }

    [Fact]
    public async Task GetBudgets_UnknownDepartment_Returns404InClassErrorFormat()
    {
        var response = await _client.GetAsync("/api/budget/v1/budgets/UNKNOWN_DEPT");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
        Assert.Contains("UNKNOWN_DEPT", error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    [Fact]
    public async Task GetAllocations_Emergency_ReturnsEMSAllocation()
    {
        var response = await _client.GetAsync("/api/budget/v1/allocations/emergency");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var allocation = await response.Content.ReadFromJsonAsync<DepartmentAllocation>();
        Assert.NotNull(allocation);
        Assert.Equal("EMS", allocation.DepartmentCode);
        Assert.Equal("Emergency Services", allocation.DepartmentName);
        Assert.Equal(4_500_000m, allocation.Allocated);
        Assert.Equal("CAD", allocation.Currency);
    }

    [Fact]
    public async Task GetAllocations_UnknownDepartment_Returns404InClassErrorFormat()
    {
        var response = await _client.GetAsync("/api/budget/v1/allocations/xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("not_found", error.Error);
        Assert.Contains("xyz", error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    [Fact]
    public async Task PostExpenses_BrokenJson_Returns400InClassErrorFormat()
    {
        var content = new StringContent("{ broken json : true ", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/budget/v1/expenses", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.NotNull(error);
        Assert.Equal("bad_request", error.Error);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }
}
