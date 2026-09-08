using FluentValidation;
using Failsafe.Application.Exceptions;
using FluentValidation;
using Failsafe.Application.Exceptions;
using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers.DTOs;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Failsafe.Application.Providers;

public class ProviderService
{
    private readonly IPaymentProviderRepository _providers;
    private readonly IHealthCheckResultRepository _healthChecks;
    private readonly IIncidentRepository _incidents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProviderRequest> _createValidator;
    private readonly IValidator<UpdateProviderRequest> _updateValidator;
    private readonly ProviderHealthEvaluator _healthEvaluator;
    private readonly ProviderHealthScoreCalculator _healthScoreCalculator;
    private readonly ILogger<ProviderService> _logger;

    public ProviderService(
        IPaymentProviderRepository providers,
        IHealthCheckResultRepository healthChecks,
        IIncidentRepository incidents,
        IUnitOfWork unitOfWork,
        IValidator<CreateProviderRequest> createValidator,
        IValidator<UpdateProviderRequest> updateValidator,
        ProviderHealthEvaluator healthEvaluator,
        ProviderHealthScoreCalculator healthScoreCalculator,
        ILogger<ProviderService> logger)
    {
        _providers = providers;
        _healthChecks = healthChecks;
        _incidents = incidents;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _healthEvaluator = healthEvaluator;
        _healthScoreCalculator = healthScoreCalculator;
        _logger = logger;
    }

    public async Task<ProviderResponse> RegisterAsync(
        CreateProviderRequest request, string createdByUserId, string createdByName, CancellationToken ct = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new FluentValidation.ValidationException(validationResult.Errors);

        var providerType = Enum.Parse<ProviderType>(request.ProviderType, ignoreCase: true);
        var provider = PaymentProvider.Register(
            request.Name, providerType, request.Priority, request.CostPerTransactionCents,
            createdByUserId, createdByName);

        await _providers.AddAsync(provider, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ToResponseAsync(provider, ct);
    }

    public async Task<IReadOnlyList<ProviderResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var providers = await _providers.GetAllAsync(ct);
        var responses = new List<ProviderResponse>();
        foreach (var provider in providers)
            responses.Add(await ToResponseAsync(provider, ct));
        return responses;
    }

    public async Task<ProviderResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Provider {id} does not exist.");

        return await ToResponseAsync(provider, ct);
    }

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

    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Provider {id} does not exist.");

        provider.Disable();
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Converts a PaymentProvider entity into its public-facing DTO shape,
    /// including live-computed status, health score, uptime %, and recent
    /// incident count — all derived from stored HealthCheckResult/Incident
    /// data, never persisted fields that could go stale.
    /// </summary>
    private async Task<ProviderResponse> ToResponseAsync(PaymentProvider provider, CancellationToken ct)
    {
        var recentResults = await _healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
        var status = _healthEvaluator.Evaluate(recentResults);
        var healthScore = _healthScoreCalculator.Calculate(recentResults);

        var uptimePercent = recentResults.Count > 0
            ? Math.Round(recentResults.Count(r => r.IsSuccessful) / (double)recentResults.Count * 100, 1)
            : 100.0;

        // Recurring-issue signal: how many incidents has this provider had
        // in the last 30 days — a provider with 3+ incidents recently is
        // worth flagging distinctly from one with a single isolated blip.
        var allIncidents = await _incidents.GetAllAsync(ct);
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentIncidentCount = allIncidents.Count(i => i.ProviderId == provider.Id && i.StartedAt >= thirtyDaysAgo);

        return new ProviderResponse(
            provider.Id, provider.Name, provider.ProviderType.ToString(),
            provider.Priority, provider.CostPerTransactionCents, provider.Enabled,
            status.ToString(), provider.CreatedByName,
            healthScore, uptimePercent, recentIncidentCount);
    }
}