namespace Failsafe.Application.Interfaces;

// Commits changes across multiple repositories as a single transaction —
// matters here since the health-check background service will often
// update a HealthCheckResult AND open/resolve an Incident together.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}