using FluentValidation;
using Failsafe.Application.Exceptions;
using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers.DTOs;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Failsafe.Application.Providers;

/// <summary>
/// Orchestrates Payment Provider use cases: converting between the raw
/// DTOs that cross the HTTP boundary and the rich PaymentProvider entity
/// that enforces the actual business rules.
/// </summary>
public class ProviderService
{
    private readonly IPaymentProviderRepository _providers;
    private readonly IHealthCheckResultRepository _healthChecks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProviderRequest> _createValidator;
    private readonly IValidator<UpdateProviderRequest> _updateValidator;
    private readonly ProviderHealthEvaluator _healthEvaluator;
    private readonly ILogger<ProviderService> _logger;

    public ProviderService(
        IPaymentProviderRepository providers,
        IHealthCheckResultRepository healthChecks,
        IUnitOfWork unitOfWork,
        IValidator<CreateProviderRequest> createValidator,
        IValidator<UpdateProviderRequest> updateValidator,
        ProviderHealthEvaluator healthEvaluator,
        ILogger<ProviderService> logger)
    {
        _providers = providers;
        _healthChecks = healthChecks;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _healthEvaluator = healthEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new payment provider. Converts the raw request DTO into
    /// a real PaymentProvider entity via its factory method, persists it,
    /// and returns a response DTO — the entity itself never leaves this method.
    /// </summary>
    public async Task<ProviderResponse> RegisterAsync(CreateProviderRequest request, CancellationToken ct = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new FluentValidation.ValidationException(validationResult.Errors);

        var providerType = Enum.Parse<ProviderType>(request.ProviderType, ignoreCase: true);
        var provider = PaymentProvider.Register(request.Name, providerType, request.Priority, request.CostPerTransactionCents);

        await _providers.AddAsync(provider, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ToResponseAsync(provider, ct);
    }

    /// <summary>
    /// Returns every registered provider, including disabled ones — used
    /// by the Admin management page, which needs to see and re-enable
    /// disabled providers, not just the active routing pool.
    /// </summary>
    public async Task<IReadOnlyList<ProviderResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var providers = await _providers.GetAllAsync(ct);
        var responses = new List<ProviderResponse>();
        foreach (var provider in providers)
            responses.Add(await ToResponseAsync(provider, ct));
        return responses;
    }

    /// <summary>
    /// Updates a provider's mutable routing configuration (Priority, Cost,
    /// Enabled). Validated first, then each field change goes through the
    /// entity's own dedicated methods. A cost change is logged before it's
    /// applied, since CostPerTransactionCents represents a negotiated
    /// contractual rate worth a lightweight audit trail.
    /// </summary>
    public async Task<ProviderResponse> UpdateAsync(Guid id, UpdateProviderRequest request, CancellationToken ct = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new FluentValidation.ValidationException(validationResult.Errors);

        var provider = await _providers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Provider {id} does not exist.");

        if (provider.CostPerTransactionCents != request.CostPerTransactionCents)
        {
            _logger.LogInformation(
                "Provider {ProviderName} cost changed from {OldCost} to {NewCost} cents",
                provider.Name, provider.CostPerTransactionCents, request.CostPerTransactionCents);
        }

        provider.ChangePriority(request.Priority);
        provider.ChangeCost(request.CostPerTransactionCents);

        if (request.Enabled) provider.Enable();
        else provider.Disable();

        await _unitOfWork.SaveChangesAsync(ct);
        return await ToResponseAsync(provider, ct);
    }

    /// <summary>
    /// Disables a provider, removing it from routing consideration without
    /// a hard delete.
    /// </summary>
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Provider {id} does not exist.");

        provider.Disable();
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Converts a PaymentProvider entity into its public-facing DTO shape,
    /// including its live computed status.
    /// </summary>
    private async Task<ProviderResponse> ToResponseAsync(PaymentProvider provider, CancellationToken ct)
    {
        var recentResults = await _healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
        var status = _healthEvaluator.Evaluate(recentResults);

        return new ProviderResponse(
            provider.Id, provider.Name, provider.ProviderType.ToString(),
            provider.Priority, provider.CostPerTransactionCents, provider.Enabled,
            status.ToString());
    }
}