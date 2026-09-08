using Failsafe.Domain.Entities;

namespace Failsafe.Domain.Services;

// Pure Domain logic, zero I/O — combines recent success rate and average
// response time into a single 0-100 score. Kept as its own class rather
// than folded into ProviderHealthEvaluator, since that class answers a
// different question (discrete Healthy/Warning/Offline state for routing
// decisions) from this one (a continuous quality signal for dashboards).
public class ProviderHealthScoreCalculator
{
    // Response times at or below this are treated as "no penalty" —
    // anything slower gradually reduces the score. A fixed reference
    // point rather than a per-provider setting, kept simple for MVP.
    private const int BaselineResponseTimeMs = 200;
    private const int WorstCaseResponseTimeMs = 3000;

    public int Calculate(IReadOnlyList<HealthCheckResult> recentResults)
    {
        if (recentResults.Count == 0)
            return 100; // no data yet — assume best case, same convention as ProviderHealthEvaluator

        var successRate = recentResults.Count(r => r.IsSuccessful) / (double)recentResults.Count;

        // Only successful checks count toward the latency component —
        // a failed check's response time isn't a meaningful "how fast is
        // this provider" signal.
        var successfulResults = recentResults.Where(r => r.IsSuccessful).ToList();
        var avgResponseTime = successfulResults.Count > 0
            ? successfulResults.Average(r => r.ResponseTimeMs)
            : WorstCaseResponseTimeMs;

        // Linearly scales latency's contribution: at or below baseline,
        // full marks; at or above worst-case, zero; interpolated between.
        var latencyScore = Math.Clamp(
            1.0 - (avgResponseTime - BaselineResponseTimeMs) / (WorstCaseResponseTimeMs - BaselineResponseTimeMs),
            0.0, 1.0);

        // Weighted 70/30 toward success rate — a provider that's mostly
        // failing shouldn't score well just because its rare successes
        // are fast.
        var score = (successRate * 0.7 + latencyScore * 0.3) * 100;

        return (int)Math.Round(score);
    }
}