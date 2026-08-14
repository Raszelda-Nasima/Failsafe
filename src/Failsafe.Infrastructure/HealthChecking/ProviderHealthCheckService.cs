using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Failsafe.Application.Interfaces;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Services;

namespace Failsafe.Infrastructure.HealthChecking;

// Runs continuously for the app's lifetime, independent of any HTTP
// request. Periodically checks every enabled provider, records the result,
// and opens/resolves Incidents based on the computed status — the actual
// engine behind the whole monitoring concept.
public class ProviderHealthCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProviderHealthCheckService> _logger;

    // A fixed interval for MVP simplicity — a real production system might
    // vary this per provider, but a single global interval is proportionate
    // to this timeline and still fully demonstrates the concept.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    public ProviderHealthCheckService(IServiceProvider serviceProvider, ILogger<ProviderHealthCheckService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProvidersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // A single failed check cycle must never crash the whole
                // background service — log it and keep running, since the
                // next cycle will simply try again.
                _logger.LogError(ex, "Error during provider health check cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckAllProvidersAsync(CancellationToken ct)
    {
        // BackgroundService is a singleton, but DbContext (via our
        // repositories) is scoped and not thread-safe to reuse across
        // calls. Creating a fresh scope per cycle is the standard,
        // required pattern for a background service that needs scoped
        // services like a DbContext.
        using var scope = _serviceProvider.CreateScope();
        var providers = scope.ServiceProvider.GetRequiredService<IPaymentProviderRepository>();
        var healthChecks = scope.ServiceProvider.GetRequiredService<IHealthCheckResultRepository>();
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var evaluator = scope.ServiceProvider.GetRequiredService<ProviderHealthEvaluator>();

        var enabledProviders = await providers.GetEnabledOrderedByPriorityAsync(ct);

        foreach (var provider in enabledProviders)
        {
            var result = SimulateHealthCheck(provider.Id);
            await healthChecks.AddAsync(result, ct);

            var recentResults = await healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
            var status = evaluator.Evaluate(recentResults);

            await ReconcileIncidentAsync(provider.Id, status, incidents, ct);

            _logger.LogInformation(
                "Health check: {ProviderName} — {Status} ({ResponseTimeMs}ms, success={IsSuccessful})",
                provider.Name, status, result.ResponseTimeMs, result.IsSuccessful);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    // Opens an Incident the moment a provider is no longer Healthy, and
    // resolves it the moment it recovers. GetOpenIncidentForProviderAsync
    // prevents duplicate incidents from stacking up across consecutive
    // unhealthy checks.
    private static async Task ReconcileIncidentAsync(
        Guid providerId, Domain.Enums.ProviderStatus status,
        IIncidentRepository incidents, CancellationToken ct)
    {
        var openIncident = await incidents.GetOpenIncidentForProviderAsync(providerId, ct);

        if (status != Domain.Enums.ProviderStatus.Healthy && openIncident is null)
        {
            var incident = Incident.Open(providerId, $"Provider health degraded to {status}");
            await incidents.AddAsync(incident, ct);
        }
        else if (status == Domain.Enums.ProviderStatus.Healthy && openIncident is not null)
        {
            openIncident.Resolve();
        }
    }

    // Simulated health check — this is explicitly NOT calling real
    // Visa/Mastercard/PayPal APIs (deliberately out of scope, see the
    // project plan). Randomized to produce a believable mix of successes,
    // slow responses, and occasional failures for demo purposes.
    private static HealthCheckResult SimulateHealthCheck(Guid providerId)
    {
        var random = Random.Shared;
        var isSuccessful = random.NextDouble() > 0.08; // ~8% baseline failure rate
        var responseTimeMs = isSuccessful ? random.Next(20, 300) : random.Next(500, 3000);

        return HealthCheckResult.Record(providerId, responseTimeMs, isSuccessful);
    }
}