using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Failsafe.Web.Services;

public class TokenMessageHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserTokenProvider _tokenProvider;
    private readonly ILogger<TokenMessageHandler> _logger;

    public TokenMessageHandler(IHttpContextAccessor httpContextAccessor, CurrentUserTokenProvider tokenProvider, ILogger<TokenMessageHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = null;

        // Prefer the token from the current HttpContext when available (prerender / initial request).
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            token = await ctx.GetTokenAsync("access_token");
        }

        // Fallback to the scoped cached token (populated by MainLayout during prerender).
        if (string.IsNullOrEmpty(token))
        {
            token = _tokenProvider?.AccessToken;
        }

        if (!string.IsNullOrEmpty(token))
        {
            // Don't log the token itself. Log source and length for diagnostics.
            var source = ctx is not null ? "HttpContext" : "TokenProvider";
            _logger.LogDebug("Attaching bearer token from {Source} (length={Length})", source, token.Length);

            // Parse the token payload without validation to extract the "aud" claim
            // for diagnostic purposes only (do not log the full token).
            try
            {
                var parts = token.Split('.');
                if (parts.Length >= 2)
                {
                    var payload = parts[1];
                    // Base64 URL decode
                    payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                    payload = payload.Replace('-', '+').Replace('_', '/');
                    var bytes = Convert.FromBase64String(payload);
                    using var doc = System.Text.Json.JsonDocument.Parse(bytes);
                    if (doc.RootElement.TryGetProperty("aud", out var aud))
                    {
                        string audValue = aud.ValueKind == System.Text.Json.JsonValueKind.Array
                            ? string.Join(",", aud.EnumerateArray().Select(a => a.GetString()))
                            : aud.GetString() ?? "";
                        _logger.LogDebug("Access token audience: {Aud}", audValue);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse access token payload for diagnostics");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogDebug("No access token available to attach to outgoing request to {Url}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
