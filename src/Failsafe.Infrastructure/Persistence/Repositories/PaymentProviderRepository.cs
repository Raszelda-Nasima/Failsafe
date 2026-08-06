using Microsoft.EntityFrameworkCore;
using Failsafe.Application.Interfaces;
using Failsafe.Domain.Entities;

namespace Failsafe.Infrastructure.Persistence.Repositories;

public class PaymentProviderRepository : IPaymentProviderRepository
{
    private readonly FailsafeDbContext _context;
    public PaymentProviderRepository(FailsafeDbContext context) => _context = context;

    public async Task<PaymentProvider?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PaymentProviders.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PaymentProvider>> GetAllAsync(CancellationToken ct = default)
        => await _context.PaymentProviders.ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentProvider>> GetEnabledOrderedByPriorityAsync(CancellationToken ct = default)
        => await _context.PaymentProviders
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

    public async Task AddAsync(PaymentProvider provider, CancellationToken ct = default)
        => await _context.PaymentProviders.AddAsync(provider, ct);
}