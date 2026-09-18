using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Failsafe.Web.Models;

namespace Failsafe.Web.Services;

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

    public async Task<List<IncidentDto>> GetIncidentsAsync()
    {
        AttachToken();
        var result = await _http.GetFromJsonAsync<List<IncidentDto>>("api/incidents", JsonOptions);
        return result ?? new List<IncidentDto>();
    }

    /// <summary>
    /// Calls the failover selection endpoint for a given network. Returns
    /// null on a 503 (no provider available) rather than throwing, since
    /// that's an expected, displayable outcome for the demo widget, not
    /// an application error.
    /// </summary>
    public async Task<RouteResultDto?> RouteTransactionAsync(string network)
    {
        AttachToken();
        var response = await _http.GetAsync($"api/paymentproviders/active?network={network}");

        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RouteResultDto>(body, JsonOptions);
    }

    /// <summary>
    /// DEMO/TESTING ONLY — injects a burst of failed health checks for a
    /// provider, triggering a visible status change on the next
    /// background check cycle. Admin-only on the API side.
    /// </summary>
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
}