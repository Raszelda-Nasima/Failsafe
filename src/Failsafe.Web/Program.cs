using Failsafe.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Authentication: a cookie holds the local session once Keycloak confirms
// identity, and OpenID Connect performs the actual authentication against
// Keycloak using the standard Authorization Code flow. This client is
// confidential (holds a real client secret) because Blazor Server executes
// entirely on the server; unlike a browser-executed SPA, there is no
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

    // Persists the access token on the authentication session so it can
    // later be attached to outgoing calls to the Failsafe API.
    options.SaveTokens = true;

    // Requests the "roles" scope from Keycloak so the realm role list is
    // included in the returned claims.
    options.Scope.Add("roles");

    options.Events = new OpenIdConnectEvents
    {
        // Keycloak returns realm roles as a nested JSON object
        // (realm_access.roles) rather than individual role claims. This
        // flattens that structure into standard ClaimTypes.Role claims,
        // which is what ASP.NET Core's [Authorize(Roles = "...")] and
        // <AuthorizeView Roles="..."> both expect.
        OnTokenValidated = context =>
        {
            var realmAccessClaim = context.Principal?.FindFirst("realm_access");
            if (realmAccessClaim is not null)
            {
                using var doc = JsonDocument.Parse(realmAccessClaim.Value);
                if (doc.RootElement.TryGetProperty("roles", out var roles))
                {
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var role in roles.EnumerateArray())
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()!));
                    }
                }
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Makes the current authentication state available to every Razor
// component in the tree via a cascading parameter, without each component
// needing to resolve it manually.
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Authentication must run before Authorization: a request's identity has
// to be established before any role/policy check against that identity
// can be evaluated.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Triggers the OpenID Connect challenge, redirecting the browser to
// Keycloak's login page. redirectUri controls where the user lands after
// a successful login.
app.MapGet("/login", (string? redirectUri) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri ?? "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]));

// Clears both the local cookie session and the Keycloak-side session.
app.MapPost("/logout", () =>
    Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.Run();