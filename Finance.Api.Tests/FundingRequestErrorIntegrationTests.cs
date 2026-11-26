using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class FundingRequestErrorIntegrationTests
{
    [Fact]
    public async Task Approve_WhenBudgetServiceThrows_ReturnsStandard500()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IBudgetFundingService>();
                    services.AddSingleton<IBudgetFundingService>(
                        new ThrowingBudgetFundingService());
                });
            });

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        const string route = "/api/budget/v1/funding-requests";

        using var created = await client.PostAsJsonAsync(route, new
        {
            departmentCode = "EMS",
            amount = 100m,
            reason = "BF-23 integration test",
            urgent = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var funding = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        var id = funding.RootElement.GetProperty("id").GetGuid();

        using var response = await client.PutAsJsonAsync(
            $"{route}/{id}/decision", new { approved = true });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var error = body.RootElement;

        Assert.Equal("internal_error", error.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("traceId").GetString()));
    }

    private sealed class ThrowingBudgetFundingService : IBudgetFundingService
    {
        public void AddFunds(string departmentCode, decimal amount)
        {
            throw new InvalidOperationException("Simulated budget service failure.");
        }
    }
}