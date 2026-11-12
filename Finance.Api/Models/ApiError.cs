using System.Text.Json.Serialization;

namespace Finance.Api.Models;

public record ApiError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("traceId")] string TraceId)
{
    public static ApiError NotFound(string message, string? traceId = null) =>
        new("not_found", message, traceId ?? Guid.NewGuid().ToString("n"));

    public static ApiError BadRequest(string message, string? traceId = null) =>
        new("bad_request", message, traceId ?? Guid.NewGuid().ToString("n"));

    public static ApiError Conflict(string message, string? traceId = null) =>
        new("conflict", message, traceId ?? Guid.NewGuid().ToString("n"));

    public static ApiError InternalServerError(string message = "An unexpected server error occurred.", string? traceId = null) =>
        new("internal_error", message, traceId ?? Guid.NewGuid().ToString("n"));
}
