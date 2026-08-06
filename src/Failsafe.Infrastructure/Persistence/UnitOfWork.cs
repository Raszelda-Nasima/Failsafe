using Failsafe.Application.Interfaces;

namespace Failsafe.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly FailsafeDbContext _context;
    public UnitOfWork(FailsafeDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
