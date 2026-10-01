using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Failsafe.Web.Models;

namespace Failsafe.Web.Services;

// Thin wrapper around HttpClient calling Failsafe.API. Every method
// attaches the current circuit's cached access token first — see
// CurrentUserTokenProvider for why the token is captured once in
// MainLayout rather than read from HttpContext here.
public class FailsafeApiClient
{
    private readonly HttpClient _http;
    private readonly CurrentUserTokenProvider _tokenProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FailsafeApiClient(HttpClient http, CurrentUserTokenProvider tokenProvider)
    {
        _http = http;
        _tokenProvider = tokenProvider;
    }

    private void AttachToken()
    {
        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken);
        }
    }

    public async Task<List<ProviderDto>> GetProvidersAsync()
    {
        AttachToken();
        var result = await _http.GetFromJsonAsync<List<ProviderDto>>("api/paymentproviders", JsonOptions);
        return result ?? new List<ProviderDto>();
    }

    public async Task<ProviderDto?> GetProviderByIdAsync(Guid id)
    {
        AttachToken();
        return await _http.GetFromJsonAsync<ProviderDto>($"api/paymentproviders/{id}", JsonOptions);
    }

    public async Task<List<IncidentDto>> GetIncidentsAsync()
    {
        AttachToken();
        var result = await _http.GetFromJsonAsync<List<IncidentDto>>("api/incidents", JsonOptions);
        return result ?? new List<IncidentDto>();
    }

    // Register: returns (success, errorMessage) rather than throwing, so
    // the form can display a friendly validation message inline instead
    // of crashing the page on a 400 from FluentValidation.
    public async Task<(bool Success, string? Error)> RegisterProviderAsync(
        string name, string providerType, int priority, int costPerTransactionCents)
    {
        AttachToken();
        var response = await _http.PostAsJsonAsync("api/paymentproviders", new
        {
            Name = name,
            ProviderType = providerType,
            Priority = priority,
            CostPerTransactionCents = costPerTransactionCents
        });

        if (response.IsSuccessStatusCode) return (true, null);

        var body = await response.Content.ReadAsStringAsync();
        return (false, ExtractErrorMessage(body));
    }

    public async Task<(bool Success, string? Error)> UpdateProviderAsync(
        Guid id, int priority, int costPerTransactionCents, bool enabled)
    {
        AttachToken();
        var response = await _http.PutAsJsonAsync($"api/paymentproviders/{id}", new
        {
            Priority = priority,
            CostPerTransactionCents = costPerTransactionCents,
            Enabled = enabled
        });

        if (response.IsSuccessStatusCode) return (true, null);

        var body = await response.Content.ReadAsStringAsync();
        return (false, ExtractErrorMessage(body));
    }

    public async Task DisableProviderAsync(Guid id)
    {
        AttachToken();
        var response = await _http.DeleteAsync($"api/paymentproviders/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ProblemDetails (from GlobalExceptionHandler) and FluentValidation's
    // own error shape differ slightly — this pulls out whichever "title"
    // or "detail" field is present, falling back to the raw body if the
    // shape doesn't match either, so an error is always shown, never lost.
    private static string ExtractErrorMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("detail", out var detail)) return detail.GetString() ?? responseBody;
            if (doc.RootElement.TryGetProperty("title", out var title)) return title.GetString() ?? responseBody;
        }
        catch (JsonException) { /* not JSON — fall through to raw body */ }
        return responseBody;
    }

    public async Task<RouteResultDto?> RouteTransactionAsync(string network)
    {
        AttachToken();
        var response = await _http.GetAsync($"api/paymentproviders/active?network={network}");
        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) return null;
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RouteResultDto>(body, JsonOptions);
    }

    public async Task SimulateFailureAsync(Guid providerId)
    {
        AttachToken();
        var response = await _http.PostAsync($"api/paymentproviders/{providerId}/simulate-failure", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task SimulateRecoveryAsync(Guid providerId)
    {
        AttachToken();
        var response = await _http.PostAsync($"api/paymentproviders/{providerId}/simulate-recovery", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MonthlySummaryDto?> GetMonthlySummaryAsync()
    {
        AttachToken();
        return await _http.GetFromJsonAsync<MonthlySummaryDto>("api/incidents/summary", JsonOptions);
    }
}