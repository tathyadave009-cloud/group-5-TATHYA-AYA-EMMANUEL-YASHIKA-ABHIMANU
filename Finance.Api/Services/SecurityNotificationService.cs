
using System.Net.Http.Json;

namespace Finance.Api.Services;

// Information sent to Security about an overdue fine.
public record OverdueFineNotice(
    string ResidentId,
    Guid FineId,
    decimal Amount,
    DateOnly DueDate,
    string Reason);

public class SecurityNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityNotificationService> _logger;

    public SecurityNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SecurityNotificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    // Sends an overdue fine notice to the configured Security service.
    public async Task<string> SendOverdueNoticeAsync(
        OverdueFineNotice notice)
    {
        string? baseUrl = _configuration["Security:BaseUrl"];
        string? path = _configuration["Security:OverdueNoticePath"];

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning(
                "Security service URL is not configured.");

            return "Failed";
        }

        try
        {
            string url = $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

            using var response = await _httpClient.PostAsJsonAsync(
                url, notice);

            if (response.IsSuccessStatusCode)
            {
                return "Sent";
            }

            _logger.LogWarning(
                "Security rejected fine notice {FineId}. Status: {StatusCode}",
                notice.FineId,
                response.StatusCode);

            return "Failed";
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is OperationCanceledException ||
            ex is InvalidOperationException ||
            ex is UriFormatException)
        {
            _logger.LogWarning(
                ex,
                "Unable to send overdue notice for fine {FineId}",
                notice.FineId);

            return "Failed";
        }
    }
}
