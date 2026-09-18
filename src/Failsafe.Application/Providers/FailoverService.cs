using Failsafe.Application.Interfaces;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;

namespace Failsafe.Application.Providers;

// Orchestrates the failover use case for a specific payment network. A
// card network (Visa, Mastercard, etc.) is fixed by the customer's card
// and cannot be substituted — this service only ever selects among
// providers of the SAME ProviderType as the request, never across
// networks. Real-world redundancy comes from registering multiple
// providers of the same type (e.g. two independent Visa processors), not
// from treating different payment methods as interchangeable fallbacks.
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

    public async Task<Domain.Entities.PaymentProvider?> SelectActiveProviderAsync(
        ProviderType requestedType, CancellationToken ct = default)
    {
        var enabledProviders = await _providers.GetEnabledOrderedByPriorityAsync(ct);

        // Filter to only providers capable of handling this specific
        // network — this is the correction that makes the system's
        // routing behavior match how card networks actually work.
        var matchingProviders = enabledProviders.Where(p => p.ProviderType == requestedType).ToList();

        var candidates = new List<ProviderCandidate>();
        foreach (var provider in matchingProviders)
        {
            var recentResults = await _healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
            var status = _healthEvaluator.Evaluate(recentResults);
            candidates.Add(new ProviderCandidate(provider, status));
        }

        return _selector.SelectProvider(candidates);
    }

    /// <summary>
    /// Same selection logic as SelectActiveProviderAsync, but also returns
    /// every candidate considered with its status — lets the caller show
    /// WHY a given provider was chosen (or skipped), for demo/audit purposes.
    /// </summary>
    public async Task<(PaymentProvider? Selected, List<object> Candidates)> SelectActiveProviderWithCandidatesAsync(
        ProviderType requestedType, CancellationToken ct = default)
    {
        var enabledProviders = await _providers.GetEnabledOrderedByPriorityAsync(ct);
        var matchingProviders = enabledProviders.Where(p => p.ProviderType == requestedType).ToList();

        var candidates = new List<ProviderCandidate>();
        foreach (var provider in matchingProviders)
        {
            var recentResults = await _healthChecks.GetRecentByProviderIdAsync(provider.Id, count: 20, ct);
            var status = _healthEvaluator.Evaluate(recentResults);
            candidates.Add(new ProviderCandidate(provider, status));
        }

        var selected = _selector.SelectProvider(candidates);

        var candidateSummaries = candidates
            .OrderBy(c => c.Provider.Priority)
            .Select(c => (object)new
            {
                c.Provider.Id,
                c.Provider.Name,
                c.Provider.Priority,
                Status = c.Status.ToString(),
                Selected = selected is not null && c.Provider.Id == selected.Id
            })
            .ToList();

        return (selected, candidateSummaries);
    }
}