using System.Text.Json;
using Finance.Api.Models;
using Xunit;

namespace Finance.Api.Tests;

public class ApiErrorTests
{
    [Fact]
    public void FactoryMethods_CreateExpectedApiErrors()
    {
        var notFound = ApiError.NotFound("Unknown dept", "trace-1");
        Assert.Equal("not_found", notFound.Error);
        Assert.Equal("Unknown dept", notFound.Message);
        Assert.Equal("trace-1", notFound.TraceId);

        var badRequest = ApiError.BadRequest("Invalid input", "trace-2");
        Assert.Equal("bad_request", badRequest.Error);
        Assert.Equal("Invalid input", badRequest.Message);
        Assert.Equal("trace-2", badRequest.TraceId);

        var conflict = ApiError.Conflict("Already decided", "trace-3");
        Assert.Equal("conflict", conflict.Error);
        Assert.Equal("Already decided", conflict.Message);
        Assert.Equal("trace-3", conflict.TraceId);

        var internalError = ApiError.InternalServerError("Crash", "trace-4");
        Assert.Equal("internal_error", internalError.Error);
        Assert.Equal("Crash", internalError.Message);
        Assert.Equal("trace-4", internalError.TraceId);
    }

    [Fact]
    public void SerializesToExpectedClassFormat()
    {
        var error = new ApiError("not_found", "Unknown department 'XYZ'.", "12345");
        string json = JsonSerializer.Serialize(error);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("not_found", root.GetProperty("error").GetString());
        Assert.Equal("Unknown department 'XYZ'.", root.GetProperty("message").GetString());
        Assert.Equal("12345", root.GetProperty("traceId").GetString());
    }
}
