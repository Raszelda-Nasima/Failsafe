using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace Failsafe.API.Controllers;

/// <summary>
/// Read-only access to incident history — auto-created and resolved by
/// ProviderHealthCheckService, never manually created via this API.
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
        // Mapped inline rather than via a dedicated DTO/service — this is
        // a simple, read-only list with no business logic beyond exposing
        // the entity's own fields, so a full Application-layer service
        // would be ceremony without benefit here.
        var response = incidents.Select(i => new
        {
            i.Id,
            i.ProviderId,
            i.Reason,
            i.StartedAt,
            i.ResolvedAt,
            DurationSeconds = i.Duration?.TotalSeconds
        });
        return Ok(response);
    }
}