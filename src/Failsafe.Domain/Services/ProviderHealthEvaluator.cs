using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;

namespace Failsafe.Domain.Services;

// Pure Domain logic, still with zero external dependencies. Thresholds
// are now constructor parameters instead of hardcoded constants, so they
// can be configured via appsettings.json without Domain ever knowing
// configuration exists — Infrastructure/API read the config values and
// pass them in as plain doubles when this class is constructed.
public class ProviderHealthEvaluator
{
    private readonly double _warningFailureRateThreshold;
    private readonly double _offlineFailureRateThreshold;

    public ProviderHealthEvaluator(
        double warningFailureRateThreshold = 0.05,
        double offlineFailureRateThreshold = 0.20)
    {
        _warningFailureRateThreshold = warningFailureRateThreshold;
        _offlineFailureRateThreshold = offlineFailureRateThreshold;
    }

    public ProviderStatus Evaluate(IReadOnlyList<HealthCheckResult> recentResults)
    {
        if (recentResults.Count == 0)
            return ProviderStatus.Healthy;

        var failureRate = recentResults.Count(r => !r.IsSuccessful) / (double)recentResults.Count;

        if (failureRate >= _offlineFailureRateThreshold) return ProviderStatus.Offline;
        if (failureRate >= _warningFailureRateThreshold) return ProviderStatus.Warning;
        return ProviderStatus.Healthy;
    }
}