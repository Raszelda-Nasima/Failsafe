using System.Linq;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;

namespace Failsafe.Domain.Services;

// Pure Domain logic: given providers ordered by priority, each paired with
// its live status, decide which one a new transaction should route to.
// No repository, no database, no I/O — fully testable with plain in-memory
// objects, same discipline as ProviderHealthEvaluator.
public class FailoverSelector
{
    // Selection is responsible for choosing the best provider by health
    // and then by configured Priority (lower number = higher priority).
    // The Application layer is still responsible for filtering to Enabled
    // providers and assembling the candidates; selector must be robust to
    // any input ordering, so it orders by Priority itself.
    public PaymentProvider? SelectProvider(IReadOnlyList<ProviderCandidate> candidates)
    {
        if (candidates is null || candidates.Count == 0) return null;

        // Ensure deterministic ordering by configured priority (ascending:
        // lower numbers are tried first).
        var ordered = candidates.OrderBy(c => c.Provider.Priority).ToList();

        // First choice: the highest-priority provider that's fully Healthy.
        var healthy = ordered.FirstOrDefault(c => c.Status == ProviderStatus.Healthy);
        if (healthy is not null) return healthy.Provider;

        // Fallback: pick the highest-priority Warning provider, if any.
        var warning = ordered.FirstOrDefault(c => c.Status == ProviderStatus.Warning);
        if (warning is not null) return warning.Provider;

        // Every enabled provider is Offline — genuinely no one to route to.
        return null;
    }
}