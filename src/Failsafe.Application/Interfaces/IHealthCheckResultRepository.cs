using Failsafe.Domain.Entities;

namespace Failsafe.Application.Interfaces;

public interface IHealthCheckResultRepository
{
    Task AddAsync(HealthCheckResult result, CancellationToken ct = default);

    // Feeds ProviderHealthEvaluator directly — "recent" is parameterized
    // since the evaluator, not the repository, owns that business decision.
    Task<IReadOnlyList<HealthCheckResult>> GetRecentByProviderIdAsync(
        Guid providerId, int count, CancellationToken ct = default);
}