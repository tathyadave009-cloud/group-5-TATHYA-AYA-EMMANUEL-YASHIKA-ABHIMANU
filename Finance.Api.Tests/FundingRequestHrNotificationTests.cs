using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Finance.Api.Controllers;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public class FundingRequestHrNotificationTests
{
    [Theory]
    [InlineData(true, 204, "Approved", true)]
    [InlineData(false, 204, "Rejected", true)]
    [InlineData(true, 503, "Approved", false)]
    [InlineData(false, 503, "Rejected", false)]
    public async Task DecisionIsKeptWhetherHrNotificationSucceedsOrFails(
        bool approved,
        int hrStatusCode,
        string expectedDecision,
        bool expectedNotified)
    {
        using var handler = new FakeHrHandler((HttpStatusCode)hrStatusCode);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hr.example.test/")
        };

        var service = new FundingRequestService(new FakeBudgetFundingService());
        var controller = new FundingRequestsController(service);

        var created = service.Create(
            "EMS", 100m, "Sprint 3 HR notification test", false,
            "REQ-0000001", "Paramedic", 1);

        var result = await controller.DecideAsync(
            created.Id,
            new FundingDecisionRequest(approved),
            new HrClient(httpClient),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<FundingRequest>(ok.Value);
        var saved = Assert.Single(service.GetAll());

        var expectedStatus = approved
            ? FundingRequestStatus.Approved
            : FundingRequestStatus.Rejected;

        Assert.Equal(expectedStatus, returned.Status);
        Assert.Equal(expectedStatus, saved.Status);
        Assert.NotNull(saved.DecidedAtUtc);
        Assert.Equal<bool?>(expectedNotified, returned.HrNotified);
        Assert.Equal<bool?>(expectedNotified, saved.HrNotified);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Patch, handler.Method);
        Assert.Equal("/requisitions/REQ-0000001", handler.Path);

        using var payload = JsonDocument.Parse(handler.Body);
        Assert.Equal(
            expectedDecision,
            payload.RootElement.GetProperty("budgetDecision").GetString());
    }

    private sealed class FakeBudgetFundingService : IBudgetFundingService
    {
        public void AddFunds(string departmentCode, decimal amount) { }
    }

    private sealed class FakeHrHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeHrHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}