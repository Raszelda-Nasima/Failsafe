using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers;
using Failsafe.Application.Providers.DTOs;

namespace Failsafe.API.Controllers;

/// <summary>
/// Manages payment providers and exposes the failover selection endpoint.
/// Write actions (Register, Update, Disable) are Admin-only; read actions
/// are open to any authenticated role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuthenticatedUser")] // baseline: any logged-in Admin or User can read
public class PaymentProvidersController : ControllerBase
{
    private readonly ProviderService _providerService;
    public PaymentProvidersController(ProviderService providerService) => _providerService = providerService;

    /// <summary>
    /// Registers a new provider. Restricted to Admins.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Register(CreateProviderRequest request, CancellationToken ct)
    {
        var result = await _providerService.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Returns every registered provider, including disabled ones.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var results = await _providerService.GetAllAsync(ct);
        return Ok(results);
    }

    /// <summary>
    /// Returns a single provider by Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _providerService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates a provider's mutable configuration. Restricted to Admins.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, UpdateProviderRequest request, CancellationToken ct)
    {
        var result = await _providerService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Disables a provider (soft delete). Restricted to Admins.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        await _providerService.DisableAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Returns which provider is currently selected to handle a
    /// transaction on the given network, or 503 if none are available.
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveProvider(
        [FromQuery] string network,
        [FromServices] FailoverService failoverService,
        CancellationToken ct)
    {
        if (!Enum.TryParse<Failsafe.Domain.Enums.ProviderType>(network, ignoreCase: true, out var providerType))
        {
            return BadRequest(new { Message = $"Unknown network '{network}'. Valid values: {string.Join(", ", Enum.GetNames<Failsafe.Domain.Enums.ProviderType>())}" });
        }

        var provider = await failoverService.SelectActiveProviderAsync(providerType, ct);

        if (provider is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { Message = $"No available {providerType} provider — all configured providers for this network are Offline." });
        }

        return Ok(new { provider.Id, provider.Name, provider.ProviderType });
    }

    /// <summary>
    /// DEMO/TESTING ONLY — injects a burst of synthetic failed health
    /// checks for a provider on demand, to demonstrate failover live.
    /// </summary>
    [HttpPost("{id:guid}/simulate-failure")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SimulateFailure(
        Guid id,
        [FromServices] IHealthCheckResultRepository healthChecks,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var result = Failsafe.Domain.Entities.HealthCheckResult.Record(id, responseTimeMs: 3000, isSuccessful: false);
            await healthChecks.AddAsync(result, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);

        return Ok(new { Message = "Injected 20 failed health checks. Status will update on the next background check cycle (within 15s)." });
    }

    /// <summary>
    /// DEMO/TESTING ONLY — the inverse of SimulateFailure, for recovery.
    /// </summary>
    [HttpPost("{id:guid}/simulate-recovery")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SimulateRecovery(
        Guid id,
        [FromServices] IHealthCheckResultRepository healthChecks,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var result = Failsafe.Domain.Entities.HealthCheckResult.Record(id, responseTimeMs: 50, isSuccessful: true);
            await healthChecks.AddAsync(result, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);

        return Ok(new { Message = "Injected 20 successful health checks. Status will update on the next background check cycle (within 15s)." });
    }
}
