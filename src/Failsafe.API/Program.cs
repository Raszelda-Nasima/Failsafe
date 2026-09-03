using Failsafe.Application.Interfaces;
using Failsafe.Infrastructure.Persistence;
using Failsafe.Infrastructure.Persistence.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);


// Explicitly bind to a single, fixed HTTP port on all network interfaces.
// This is required so prometheus.yml (which scrapes this exact port via
// host.docker.internal) always finds the API regardless of which launch
// profile or IDE started it, and it removes HTTPS/self-signed-certificate
// concerns entirely for local development.
builder.WebHost.UseUrls("http://0.0.0.0:5171");

// --- Persistence ---
// EF Core DbContext, pointed at SQL Server via a connection string kept in
// User Secrets locally, never committed to source control.
builder.Services.AddDbContext<FailsafeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FailsafeDb")));

// --- Dependency Injection: Application interfaces to Infrastructure implementations ---
builder.Services.AddScoped<IPaymentProviderRepository, PaymentProviderRepository>();
builder.Services.AddScoped<IHealthCheckResultRepository, HealthCheckResultRepository>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<Failsafe.Application.Providers.ProviderService>();
builder.Services.AddScoped<Failsafe.Domain.Services.ProviderHealthEvaluator>();
builder.Services.AddScoped<Failsafe.Application.Providers.FailoverService>();
builder.Services.AddScoped<Failsafe.Domain.Services.FailoverSelector>();
builder.Services.AddValidatorsFromAssembly(typeof(Failsafe.Application.Providers.ProviderService).Assembly);

// Runs continuously for the app's lifetime, periodically checking every
// enabled provider's health.
builder.Services.AddHostedService<Failsafe.Infrastructure.HealthChecking.ProviderHealthCheckService>();

builder.Services.AddExceptionHandler<Failsafe.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// --- Authentication ---
// Trusts Keycloak as the identity provider. The API never sees a password;
// it only validates JWTs Keycloak has already issued and signed.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/failsafe";
        options.Audience = "failsafe-api";
        options.RequireHttpsMetadata = false; // local dev only — Keycloak runs over plain HTTP here

        options.Events = new JwtBearerEvents
        {
            // Keycloak nests roles inside "realm_access" as raw JSON rather
            // than individual role claims. This flattens them into real
            // ClaimTypes.Role claims that RequireRole()/[Authorize] understand.
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

// --- Authorization policies ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AnyAuthenticatedUser", policy => policy.RequireRole("Admin", "User"));
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("FailsafeWebClient", policy =>
        policy.WithOrigins("http://localhost:5056") // Failsafe.Web's actual port — update if different
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// --- OpenTelemetry ---
// Instruments the API with distributed tracing and metrics, exported in
// Prometheus format on a dedicated endpoint.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Failsafe.API"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("Failsafe.Providers") // registers our custom domain metrics for export
        .AddPrometheusExporter());

// --- Controllers & Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configures Swagger's "Authorize" button to speak OAuth2 directly to
// Keycloak, rather than requiring a raw JWT fetched via curl and pasted
// in manually. DEVELOPMENT-ONLY: uses the OAuth2 Password grant, which is
// discouraged for production since a client handles the raw password
// directly. Safe here because the whole Swagger UI is gated behind
// IsDevelopment() below and never mapped outside that environment.
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Password = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("http://localhost:8080/realms/failsafe/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect scope, required by Keycloak's token endpoint" }
                }
            }
        }
    });

    // Swashbuckle 10.x / Microsoft.OpenApi 3.x: AddSecurityRequirement takes
    // a delegate receiving the in-progress document; references are built
    // via OpenApiSecuritySchemeReference, not a .Reference property (that
    // property was removed from OpenApiSecurityScheme in Microsoft.OpenApi 2.0).
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("oauth2", document)] = new List<string> { "openid" }
    });
});

// --- Build the app. Everything above this line configures services;
// everything below configures the request pipeline. This boundary must
// never be crossed or nested inside a service-configuration callback. ---
var app = builder.Build();

// Applies any pending EF Core migrations automatically on startup, in
// Development only. Convenient for local iteration; errors are logged
// rather than crashing the app, since a temporarily unreachable database
// shouldn't prevent the API from starting up for unrelated testing.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FailsafeDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EF Core migration failed on startup: {ex.Message}");
    }
}

// Exposes OpenTelemetry metrics in Prometheus's expected text format at
// /metrics — the endpoint prometheus.yml is configured to scrape.
app.MapPrometheusScrapingEndpoint();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId("failsafe-api");
        options.EnablePersistAuthorization();
    });
}

app.UseCors("FailsafeWebClient");

app.UseHttpsRedirection();
// Order matters: Authentication (who are you?) before Authorization
// (what are you allowed to do?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();