using FluentValidation;
using Failsafe.Application.Exceptions;
using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers.DTOs;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;

namespace Failsafe.Application.Providers;

public class ProviderService
{
    private readonly IPaymentProviderRepository _providers;
    private readonly IHealthCheckResultRepository _healthChecks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProviderRequest> _validator;
    private readonly ProviderHealthEvaluator _healthEvaluator;

    public ProviderService(
        IPaymentProviderRepository providers,
        IHealthCheckResultRepository healthChecks,
        IUnitOfWork unitOfWork,
        IValidator<CreateProviderRequest> validator,
        ProviderHealthEvaluator healthEvaluator)
    {
        _providers = providers;
        _healthChecks = healthChecks;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _healthEvaluator = healthEvaluator;
    }

    public async Task<ProviderResponse> RegisterAsync(CreateProviderRequest request, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new FluentValidation.ValidationException(validationResult.Errors);

        var providerType = Enum.Parse<ProviderType>(request.ProviderType, ignoreCase: true);
        var provider = PaymentProvider.Register(request.Name, providerType, request.Priority, request.CostPerTransactionCents);

        await _providers.AddAsync(provider, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ToResponseAsync(provider, ct);
    }

    public async Task<IReadOnlyList<ProviderResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var providers = await _providers.GetAllAsync(ct);
        // Sequentially awaited rather than Task.WhenAll — at MVP scale
        // (a handful of providers) the simplicity outweighs the marginal
        // parallelism gain, and it avoids surprising concurrent-DbContext
        // access, since DbContext is not thread-safe.
        var responses = new List<ProviderResponse>();
        foreach (var provider in providers)
            responses.Add(await ToResponseAsync(provider, ct));
        return responses;
    }

    // Centralizes the entity-to-DTO mapping AND the live status calculation
    // in one place — every caller gets a consistently computed Status,
    // never a stale or forgotten one.
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