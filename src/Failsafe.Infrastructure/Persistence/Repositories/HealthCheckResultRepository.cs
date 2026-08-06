using Microsoft.EntityFrameworkCore;
using Failsafe.Application.Interfaces;
using Failsafe.Domain.Entities;

namespace Failsafe.Infrastructure.Persistence.Repositories;

public class HealthCheckResultRepository : IHealthCheckResultRepository
{
    private readonly FailsafeDbContext _context;
    public HealthCheckResultRepository(FailsafeDbContext context) => _context = context;

    public async Task AddAsync(HealthCheckResult result, CancellationToken ct = default)
        => await _context.HealthCheckResults.AddAsync(result, ct);

    public async Task<IReadOnlyList<HealthCheckResult>> GetRecentByProviderIdAsync(
        Guid providerId, int count, CancellationToken ct = default)
        => await _context.HealthCheckResults
            .Where(h => h.ProviderId == providerId)
            .OrderByDescending(h => h.CheckedAt)
            .Take(count)
            .ToListAsync(ct);
}
