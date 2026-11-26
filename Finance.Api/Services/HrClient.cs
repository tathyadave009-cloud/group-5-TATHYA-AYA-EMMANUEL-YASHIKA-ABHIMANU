using System.Net.Http;
using System.Net.Http.Json;
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
}