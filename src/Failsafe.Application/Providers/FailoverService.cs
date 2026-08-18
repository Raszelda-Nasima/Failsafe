using Failsafe.Application.Interfaces;
using Failsafe.Domain.Services;

namespace Failsafe.Application.Providers;

// Orchestrates the failover use case: fetches enabled providers and their
// recent health data, builds the ProviderCandidate list FailoverSelector
// needs, and returns the chosen provider (or null if none are available).
public class FailoverService
{
    private readonly IPaymentProviderRepository _providers;
    private readonly IHealthCheckResultRepository _healthChecks;
    private readonly ProviderHealthEvaluator _healthEvaluator;
    private readonly FailoverSelector _selector;

    public FailoverService(
        IPaymentProviderRepository providers,
        IHealthCheckResultRepository healthChecks,
        ProviderHealthEvaluator healthEvaluator,
        FailoverSelector selector)
    {
        _providers = providers;
        _healthChecks = healthChecks;
        _healthEvaluator = healthEvaluator;
        _selector = selector;
    }

    public async Task<Domain.Entities.PaymentProvider?> SelectActiveProviderAsync(CancellationToken ct = default)
    {
        // Already filtered to Enabled and ordered by Priority — the
        // repository, not this method, owns that query concern.
        var enabledProviders = await _providers.GetEnabledOrderedByPriorityAsync(ct);

        var candidates = new List<ProviderCandidate>();
        foreach (var provider in enabledProviders)
        {
            var recentResults = await _healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
            var status = _healthEvaluator.Evaluate(recentResults);
            candidates.Add(new ProviderCandidate(provider, status));
        }

        // The actual decision — pure, testable Domain logic — happens here.
        return _selector.SelectProvider(candidates);
    }
}