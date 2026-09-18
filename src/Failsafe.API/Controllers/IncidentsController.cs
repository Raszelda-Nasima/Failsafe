using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers;

namespace Failsafe.API.Controllers;

/// <summary>
/// Read-only access to incident history and analytics. Incidents are
/// exclusively auto-created/resolved by ProviderHealthCheckService.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuthenticatedUser")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentRepository _incidents;
    public IncidentsController(IIncidentRepository incidents) => _incidents = incidents;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var incidents = await _incidents.GetAllAsync(ct);
        var response = incidents.Select(i => new
        {
            i.Id,
            i.ProviderId,
            i.Reason,
            i.StartedAt,
            i.ResolvedAt,
            IsOpen = !i.ResolvedAt.HasValue,
            DurationSeconds = i.Duration?.TotalSeconds
        });
        return Ok(response);
    }

    /// <summary>
    /// Full monthly analytics roundup: incident counts, resolution rate,
    /// total downtime, incidents grouped by provider, current provider
    /// health breakdown, and cost-per-provider — feeds the Monthly
    /// Roundup page's cards, bars, and donut chart.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetMonthlySummary(
        [FromServices] ProviderService providerService, CancellationToken ct)
    {
        var incidents = await _incidents.GetAllAsync(ct);
        var now = DateTime.UtcNow;

        var thisMonth = incidents
            .Where(i => i.StartedAt.Year == now.Year && i.StartedAt.Month == now.Month)
            .ToList();

        var resolved = thisMonth.Where(i => i.ResolvedAt.HasValue).ToList();
        var openCount = thisMonth.Count - resolved.Count;

        double? avgResolutionMinutes = resolved.Count > 0
            ? resolved.Average(i => i.Duration!.Value.TotalMinutes)
            : null;

        // Still-open incidents count time elapsed so far toward downtime,
        // since they're actively contributing to it right now.
        var totalDowntimeMinutes = thisMonth.Sum(i =>
            (i.ResolvedAt ?? now).Subtract(i.StartedAt).TotalMinutes);

        var providers = await providerService.GetAllAsync(ct);
        var providerLookup = providers.ToDictionary(p => p.Id, p => p.Name);

        var incidentsByProvider = thisMonth
            .GroupBy(i => providerLookup.GetValueOrDefault(i.ProviderId, "Unknown"))
            .Select(g => new { Provider = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var healthBreakdown = new
        {
            Healthy = providers.Count(p => p.Status == "Healthy"),
            Warning = providers.Count(p => p.Status == "Warning"),
            Offline = providers.Count(p => p.Status == "Offline")
        };

        var enabledProviders = providers.Where(p => p.Enabled).ToList();
        var costByProvider = enabledProviders
            .Select(p => new { p.Name, p.CostPerTransactionCents })
            .OrderByDescending(p => p.CostPerTransactionCents)
            .ToList();

        var averageCostPerTransactionCents = enabledProviders.Count > 0
            ? Math.Round(enabledProviders.Average(p => p.CostPerTransactionCents), 2)
            : 0;

        var resolutionRatePercent = thisMonth.Count > 0
            ? Math.Round((double)resolved.Count / thisMonth.Count * 100, 0)
            : 0;

        return Ok(new
        {
            Month = now.ToString("MMMM yyyy"),
            TotalIncidents = thisMonth.Count,
            OpenIncidents = openCount,
            ResolvedIncidents = resolved.Count,
            ResolutionRatePercent = resolutionRatePercent,
            TotalDowntimeMinutes = Math.Round(totalDowntimeMinutes, 0),
            AverageResolutionMinutes = avgResolutionMinutes,
            AverageCostPerTransactionCents = averageCostPerTransactionCents,
            IncidentsByProvider = incidentsByProvider,
            ProviderHealthBreakdown = healthBreakdown,
            CostByProvider = costByProvider
        });
    }
}