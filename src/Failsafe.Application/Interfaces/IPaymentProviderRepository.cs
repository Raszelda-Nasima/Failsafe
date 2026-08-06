using Failsafe.Domain.Entities;

namespace Failsafe.Application.Interfaces;

// Narrow, purpose-built interface — no generic IRepository<T>, no leaked
// IQueryable; every method returns an already-materialized result.
public interface IPaymentProviderRepository
{
    Task<PaymentProvider?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentProvider>> GetAllAsync(CancellationToken ct = default);

    // Excludes disabled providers — this is what the failover selection
    // logic will actually query against.
    Task<IReadOnlyList<PaymentProvider>> GetEnabledOrderedByPriorityAsync(CancellationToken ct = default);

    Task AddAsync(PaymentProvider provider, CancellationToken ct = default);
}
