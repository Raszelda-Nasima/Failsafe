using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Failsafe.Application.Providers;
using Failsafe.Application.Providers.DTOs;

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
}