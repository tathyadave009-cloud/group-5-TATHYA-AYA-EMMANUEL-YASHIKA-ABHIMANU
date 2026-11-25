
using System.Net;
using System.Text.Json;
using Finance.Api.Controllers;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Finance.Api.Tests;

public class OverdueFineNoticeTests
{
    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public string? RequestBody { get; private set; }

        public FakeHttpHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }

    private class FailingHttpHandler : HttpMessageHandler
    {
        private readonly bool _simulateTimeout;

        public FailingHttpHandler(bool simulateTimeout)
        {
            _simulateTimeout = simulateTimeout;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_simulateTimeout)
            {
                throw new TaskCanceledException(
                    "Simulated Security timeout.");
            }

            throw new HttpRequestException(
                "Simulated Security connection failure.");
        }
    }

    private static SecurityNotificationService CreateSecurityService(
        HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:BaseUrl"] = "http://security.test",
                ["Security:OverdueNoticePath"] = "/overdue-notice"
            })
            .Build();

        return new SecurityNotificationService(
            client,
            configuration,
            NullLogger<SecurityNotificationService>.Instance);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "Sent")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Failed")]
    public async Task SendNotice_StoresCorrectStatus(
        HttpStatusCode responseCode,
        string expectedStatus)
    {
        var revenueService = new RevenueService();

        var dueDate = DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-5));

        var result = revenueService.RecordRevenue(
            "FINES",
            "SEC",
            150m,
            "Parking fine",
            "RES-001",
            dueDate);

        Assert.True(result.Approved);
        Assert.NotNull(result.Revenue);

        var handler = new FakeHttpHandler(responseCode);

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var securityService = CreateSecurityService(client);
        var controller = new RevenueController(revenueService);

        var response = await controller.SendOverdueNotice(
            result.Revenue.Id,
            securityService);

        Assert.IsType<OkObjectResult>(response);

        var updatedRevenue = revenueService.GetRevenueById(
            result.Revenue.Id);

        Assert.NotNull(updatedRevenue);
        Assert.Equal(
            expectedStatus,
            updatedRevenue.OverdueNoticeStatus);

        Assert.NotNull(handler.RequestBody);

        using var json = JsonDocument.Parse(handler.RequestBody);

        var payload = json.RootElement;

        Assert.Equal(
            "RES-001",
            payload.GetProperty("residentId").GetString());

        Assert.Equal(
            result.Revenue.Id,
            payload.GetProperty("fineId").GetGuid());

        Assert.Equal(
            150m,
            payload.GetProperty("amount").GetDecimal());

        Assert.Equal(
            dueDate.ToString("yyyy-MM-dd"),
            payload.GetProperty("dueDate").GetString());

        Assert.Equal(
            "Overdue fine",
            payload.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task SendNotice_FineNotOverdue_ReturnsBadRequest()
    {
        var revenueService = new RevenueService();

        var result = revenueService.RecordRevenue(
            "FINES",
            "SEC",
            100m,
            "Parking fine",
            "RES-002",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)));

        Assert.NotNull(result.Revenue);

        var handler = new FakeHttpHandler(HttpStatusCode.OK);

        using var client = new HttpClient(handler);

        var securityService = CreateSecurityService(client);
        var controller = new RevenueController(revenueService);

        var response = await controller.SendOverdueNotice(
            result.Revenue.Id,
            securityService);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Null(handler.RequestBody);
    }

    [Fact]
    public async Task SendNotice_UnknownFine_ReturnsNotFound()
    {
        var revenueService = new RevenueService();

        var handler = new FakeHttpHandler(HttpStatusCode.OK);

        using var client = new HttpClient(handler);

        var securityService = CreateSecurityService(client);
        var controller = new RevenueController(revenueService);

        var response = await controller.SendOverdueNotice(
            Guid.NewGuid(),
            securityService);

        Assert.IsType<NotFoundObjectResult>(response);
        Assert.Null(handler.RequestBody);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendNotice_ConnectionFailureOrTimeout_StoresFailed(
        bool simulateTimeout)
    {
        var revenueService = new RevenueService();

        var result = revenueService.RecordRevenue(
            "FINES",
            "SEC",
            200m,
            "Overdue parking fine",
            "RES-003",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));

        Assert.True(result.Approved);
        Assert.NotNull(result.Revenue);

        using var client = new HttpClient(
            new FailingHttpHandler(simulateTimeout))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var securityService = CreateSecurityService(client);
        var controller = new RevenueController(revenueService);

        var response = await controller.SendOverdueNotice(
            result.Revenue.Id,
            securityService);

        Assert.IsType<OkObjectResult>(response);

        var updatedRevenue = revenueService.GetRevenueById(
            result.Revenue.Id);

        Assert.NotNull(updatedRevenue);
        Assert.Equal("Failed", updatedRevenue.OverdueNoticeStatus);
    }
}

