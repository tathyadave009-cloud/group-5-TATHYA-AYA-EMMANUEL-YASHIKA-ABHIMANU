using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Finance.Api.Services;

public sealed class HrClient
{
    private readonly HttpClient _httpClient;

    public HrClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TryNotifyDecisionAsync(
        string requisitionNumber,
        bool approved,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requisitionNumber) ||
            _httpClient.BaseAddress is null)
        {
            return false;
        }

        string number = Uri.EscapeDataString(requisitionNumber.Trim());
        string decision = approved ? "Approved" : "Rejected";

        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"requisitions/{number}")
        {
            Content = JsonContent.Create(new { budgetDecision = decision })
        };

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
    // BF-30: headcount and salary totals per department from HR
    // (GET {Hr:BaseUrl}/headcounts/payroll). HR only shares totals, never individual salaries.
    // Returns null when HR is not configured, down, slow (5 s) or sends something that is not JSON.
    public async Task<JsonElement?> TryGetPayrollAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress is null)
            return null;

        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var response = await _httpClient.GetAsync("headcounts/payroll", timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            return document.RootElement.Clone();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
