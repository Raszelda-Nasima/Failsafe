using Failsafe.Application.Interfaces;
using Failsafe.Infrastructure.Persistence;
using Failsafe.Infrastructure.Persistence.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);


// --- Persistence ---
// EF Core DbContext, pointed at SQL Server via a connection string kept in
// User Secrets locally, never committed to source control.
builder.Services.AddDbContext<FailsafeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FailsafeDb")));

// --- Dependency Injection: Application interfaces to Infrastructure implementations ---
// Scoped lifetime matches DbContext's own recommended lifetime — one shared
// instance per request, so repositories used together in one request share
// the same DbContext and commit via IUnitOfWork as a single transaction.
builder.Services.AddScoped<IPaymentProviderRepository, PaymentProviderRepository>();
builder.Services.AddScoped<IHealthCheckResultRepository, HealthCheckResultRepository>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<Failsafe.Application.Providers.ProviderService>();
builder.Services.AddScoped<Failsafe.Domain.Services.ProviderHealthEvaluator>();
builder.Services.AddValidatorsFromAssembly(typeof(Failsafe.Application.Providers.ProviderService).Assembly);
builder.Services.AddExceptionHandler<Failsafe.API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
            // ClaimTypes.Role claims that RequireRole()/[Authorize].
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
// Named after business capability, matching the rubric's own Admin/User
// naming directly — Admin manages providers and routing priority, User
// only ever reads dashboards.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    // Both Admin and User can read — a named policy even though it's just
    // "authenticated," so controllers express intent explicitly rather
    // than relying on a bare [Authorize] that doesn't say why.
    options.AddPolicy("AnyAuthenticatedUser", policy => policy.RequireRole("Admin", "User"));
});

// --- CORS ---
// Not strictly needed yet since Blazor Server calls the API server-to-server
// (no browser-origin restriction applies there), but kept explicit and
// ready in case a future admin tool or mobile client needs it.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FailsafeWebClient", policy =>
        policy.WithOrigins("http://localhost:5056") // corrected to match Failsafe.Web's real port
              .AllowAnyHeader()
              .AllowAnyMethod());
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FailsafeWebClient");

// Order matters: Authentication (who are you?) before Authorization
// (what are you allowed to do?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();