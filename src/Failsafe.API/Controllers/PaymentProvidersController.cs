using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers;
using Failsafe.Application.Providers.DTOs;
using Failsafe.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Failsafe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuthenticatedUser")] // baseline: any logged-in Admin or User can read
public class PaymentProvidersController : ControllerBase
{
    private readonly ProviderService _providerService;
    public PaymentProvidersController(ProviderService providerService) => _providerService = providerService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _providerService.GetAllAsync(ct));

    // Registering a new provider is Admin-only — a real security-sensitive
    // action, since adding/removing providers changes transaction routing.
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Register(CreateProviderRequest request, CancellationToken ct)
    {
        var result = await _providerService.RegisterAsync(request, ct);
        return Created($"/api/paymentproviders/{result.Id}", result);
    }

    // Answers "which provider should handle a transaction on this network
    // right now?" — accepts the requested network as a required query
    // parameter, since routing is only ever meaningful within one network.
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveProvider(
        [FromQuery] string network,
        [FromServices] FailoverService failoverService,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ProviderType>(network, ignoreCase: true, out var providerType))
        {
            return BadRequest(new { Message = $"Unknown network '{network}'. Valid values: {string.Join(", ", Enum.GetNames<ProviderType>())}" });
        }

        var provider = await failoverService.SelectActiveProviderAsync(providerType, ct);

        if (provider is null)
        {
            // 503, not 200 — this is a genuine service-unavailable condition
            // for this specific network, not a normal successful response
            // that happens to carry a message.
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { Message = $"No available {providerType} provider — all configured providers for this network are Offline." });
        }

        return Ok(new { provider.Id, provider.Name, provider.ProviderType });
    }

    // DEMO/TESTING ONLY — not a real production capability. Injects a burst of
    // synthetic failed health checks for a provider on demand, so failover
    // behavior can be demonstrated live rather than waiting for the background
    // service's random ~8% failure rate to naturally trigger a status change.
    [HttpPost("{id:guid}/simulate-failure")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SimulateFailure(
        Guid id,
        [FromServices] IHealthCheckResultRepository healthChecks,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        // 20 consecutive failures guarantees ProviderHealthEvaluator's Offline
        // threshold (20% failure rate over the last 20 checks) is crossed
        // immediately, rather than needing to wait for enough real checks to
        // accumulate naturally.
        for (var i = 0; i < 20; i++)
        {
            var result = Failsafe.Domain.Entities.HealthCheckResult.Record(id, responseTimeMs: 3000, isSuccessful: false);
            await healthChecks.AddAsync(result, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);

        return Ok(new { Message = "Injected 20 failed health checks. Status will update on the next background check cycle (within 15s)." });
    }

    // DEMO/TESTING ONLY — the inverse of the above, to demonstrate recovery
    // (failback) as part of the same live story.
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


    // Updates a provider's mutable configuration. Name and ProviderType are
    // intentionally excluded — changing which network a provider serves, or
    // its identity, would silently invalidate historical HealthCheckResult
    // and Incident records tied to it.
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProviderRequest request,
        [FromServices] ProviderService providerService,
        CancellationToken ct)
    {
        var result = await providerService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    // "Deleting" a provider means disabling it, not a hard database delete.
    // A provider with any health check history (which happens within seconds
    // of registration, given the background service's 15s interval) has real
    // foreign-key-referenced rows in HealthCheckResult/Incident — deleting it
    // would either fail outright or destroy audit history. Disabling removes
    // it from routing consideration while preserving that history.
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Disable(
        Guid id,
        [FromServices] ProviderService providerService,
        CancellationToken ct)
    {
        await providerService.DisableAsync(id, ct);
        return NoContent();
    }
}

