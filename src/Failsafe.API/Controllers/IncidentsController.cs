using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Failsafe.Application.Interfaces;

namespace Failsafe.API.Controllers;

/// <summary>
/// Read-only access to incident history. Incidents are exclusively
/// auto-created and auto-resolved by ProviderHealthCheckService based on
/// failure-rate thresholds — there is no endpoint to manually create one,
/// since a human never "opens" an incident in this system's design.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuthenticatedUser")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentRepository _incidents;
    public IncidentsController(IIncidentRepository incidents) => _incidents = incidents;

    /// <summary>
    /// Returns every incident, resolved or still open, most useful for
    /// the Incidents page's full history table.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var incidents = await _incidents.GetAllAsync(ct);

        // Mapped inline rather than via a dedicated DTO/service — this is
        // a simple, read-only projection with no business logic beyond
        // exposing the entity's own fields, so a full Application-layer
        // service would be ceremony without benefit here.
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
    /// Returns a summary of incident activity for the current calendar
    /// month — total incidents, still-open count, resolved count, and
    /// average resolution time. Pure aggregation over existing Incident
    /// data; feeds the Dashboard's monthly roundup panel.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetMonthlySummary(CancellationToken ct)
    {
        var incidents = await _incidents.GetAllAsync(ct);
        var now = DateTime.UtcNow;

        var thisMonth = incidents
            .Where(i => i.StartedAt.Year == now.Year && i.StartedAt.Month == now.Month)
            .ToList();

        var resolved = thisMonth.Where(i => i.ResolvedAt.HasValue).ToList();

        // Null when nothing has resolved yet this month, rather than 0 —
        // 0 would misleadingly suggest instant resolutions, when the
        // truth is there's simply no data to average yet.
        double? avgResolutionMinutes = resolved.Count > 0
            ? resolved.Average(i => i.Duration!.Value.TotalMinutes)
            : null;

        return Ok(new
        {
            Month = now.ToString("MMMM yyyy"),
            TotalIncidents = thisMonth.Count,
            StillOpen = thisMonth.Count(i => !i.ResolvedAt.HasValue),
            Resolved = resolved.Count,
            AverageResolutionMinutes = avgResolutionMinutes
        });
    }
}