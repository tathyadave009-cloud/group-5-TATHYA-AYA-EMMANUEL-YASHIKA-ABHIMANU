using System.Net;
using System.Text;
using System.Text.Json;
using Finance.Api.Controllers;
using Finance.Api.Models;
using Finance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Finance.Api.Tests;

// BF-30: GET /api/budget/v1/hr/payroll reads HR's /headcounts/payroll.
public class HrPayrollTests
{
    private sealed class PayrollHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }

        public PayrollHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static HrController Controller(HttpMessageHandler handler, bool configured = true)
    {
        var http = new HttpClient(handler);
        if (configured)
            http.BaseAddress = new Uri("https://hr.example.test/api/hr/v1/");
        return new HrController(new HrClient(http));
    }

    [Fact]
    public async Task Payroll_HrAnswers_Returns200WithHrTotals()
    {
        var handler = new PayrollHandler(HttpStatusCode.OK,
            "[{\"departmentCode\":\"EMS\",\"headcount\":12,\"salaryCost\":840000}]");

        var result = await Controller(handler).GetPayrollAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = Assert.IsType<JsonElement>(ok.Value);
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal("EMS", json[0].GetProperty("departmentCode").GetString());
        Assert.Equal(12, json[0].GetProperty("headcount").GetInt32());
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/hr/v1/headcounts/payroll", handler.Path);
    }

    [Fact]
    public async Task Payroll_HrDown_Returns503InClassFormat()
    {
        var handler = new PayrollHandler(HttpStatusCode.InternalServerError, "{}");

        var result = await Controller(handler).GetPayrollAsync(CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        var error = Assert.IsType<ApiError>(obj.Value);
        Assert.Equal("service_unavailable", error.Error);
    }

    [Fact]
    public async Task Payroll_HrNotConfigured_Returns503WithoutCallingHr()
    {
        var handler = new PayrollHandler(HttpStatusCode.OK, "[]");

        var result = await Controller(handler, configured: false).GetPayrollAsync(CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task Payroll_HrSendsNotJson_Returns503()
    {
        var handler = new PayrollHandler(HttpStatusCode.OK, "<html>not json</html>");

        var result = await Controller(handler).GetPayrollAsync(CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }
}
