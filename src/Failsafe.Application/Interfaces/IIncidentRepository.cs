using Failsafe.Domain.Entities;

namespace Failsafe.Application.Interfaces;

public interface IIncidentRepository
{
    Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken ct = default);

    // Prevents duplicate incidents from stacking up while a provider
    // remains unhealthy across multiple consecutive health checks.
    Task<Incident?> GetOpenIncidentForProviderAsync(Guid providerId, CancellationToken ct = default);

    Task AddAsync(Incident incident, CancellationToken ct = default);
}