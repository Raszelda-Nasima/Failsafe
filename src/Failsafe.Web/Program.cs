using Failsafe.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Required for FailsafeApiClient / CurrentUserTokenProvider to access the
// current request's authentication cookie/tokens from a scoped service.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Failsafe.Web.Services.CurrentUserTokenProvider>();
builder.Services.AddHttpClient<Failsafe.Web.Services.FailsafeApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5171/");
});

// Authentication: a cookie holds the local session once Keycloak confirms
// identity, and OpenID Connect performs the actual authentication using
// the Authorization Code flow. failsafe-web is a confidential client
// (holds a real client secret) because Blazor Server executes entirely
// on the server — unlike a browser-executed SPA, there is no
// public-client/PKCE requirement here.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Keycloak:Authority"];
    options.ClientId = builder.Configuration["Keycloak:ClientId"];
    options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"];
    options.ResponseType = "code";
    options.RequireHttpsMetadata = false; // local development only

    // Persists the access token on the authentication session so
    // FailsafeApiClient can attach it to outgoing calls to the API.
    options.SaveTokens = true;

    // Requests the "roles" scope from Keycloak so realm roles are
    // included in the returned token.
    options.Scope.Add("roles");

    options.Events = new OpenIdConnectEvents
    {
        // Keycloak roles come in realm_access.roles on the access token.
        // Decode the access token payload and map those roles to
        // ClaimTypes.Role for Blazor authorization checks.
        OnTokenValidated = context =>
        {
            var accessToken = context.TokenEndpointResponse?.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                return Task.CompletedTask;
            }

            var payloadSegment = accessToken.Split('.')[1]
                .Replace('-', '+')
                .Replace('_', '/');
            var padded = payloadSegment.PadRight(
                payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4,
                '=');
            var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));

            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("realm_access", out var realmAccess) &&
                realmAccess.TryGetProperty("roles", out var roles))
            {
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                foreach (var role in roles.EnumerateArray())
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()!));
                }
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Makes the current authentication state available to every Razor
// component via a cascading parameter.
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Order matters: Authentication (who are you?) before Authorization
// (what are you allowed to do?), both before Antiforgery/routing.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Triggers the OpenID Connect challenge, redirecting to Keycloak's login page.
app.MapGet("/login", (string? redirectUri) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri ?? "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]));

// GET rather than POST — simplifies the logout link to a plain <a> tag
// without needing antiforgery-token plumbing for a form POST. A
// reasonable simplification for an internal ops tool.
app.MapGet("/logout", () =>
    Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.Run();