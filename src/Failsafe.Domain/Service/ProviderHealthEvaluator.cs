using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;

namespace Failsafe.Domain.Services;

// A Domain Service — pure business logic that doesn't belong to any single
// entity. Unlike PaymentProvider or Incident, this class has no identity
// and persists nothing; it's purely a calculation, which is exactly why it
// doesn't live as a method on PaymentProvider itself.
public class ProviderHealthEvaluator
{
    // The business rule named explicitly in the requirements: error rates
    // exceeding 5% mark a provider unhealthy. A second, stricter threshold
    // distinguishes "degraded but still usable" (Warning) from genuinely
    // "Offline" — giving the dashboard three real states, not just two.
    private const double WarningFailureRateThreshold = 0.05; // 5%
    private const double OfflineFailureRateThreshold = 0.20; // 20%

    // Takes the most recent N health checks for a provider and derives its
    // current status. Recency matters — a provider that failed constantly
    // an hour ago but has recovered since should read as Healthy now, not
    // be punished forever by old data.
    public ProviderStatus Evaluate(IReadOnlyList<HealthCheckResult> recentResults)
    {
        if (recentResults.Count == 0)
        {
            // No data yet (e.g. a newly registered provider) — treat as
            // Healthy rather than Offline, since "unknown" and "known bad"
            // are genuinely different situations for an Admin to see.
            return ProviderStatus.Healthy;
        }

        var failureRate = recentResults.Count(r => !r.IsSuccessful) / (double)recentResults.Count;

        if (failureRate >= OfflineFailureRateThreshold) return ProviderStatus.Offline;
        if (failureRate >= WarningFailureRateThreshold) return ProviderStatus.Warning;
        return ProviderStatus.Healthy;
    }
}