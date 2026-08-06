using Microsoft.EntityFrameworkCore;
using Failsafe.Application.Interfaces;
using Failsafe.Domain.Entities;

namespace Failsafe.Infrastructure.Persistence.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly FailsafeDbContext _context;
    public IncidentRepository(FailsafeDbContext context) => _context = context;

    public async Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken ct = default)
        => await _context.Incidents.ToListAsync(ct);

    public async Task<Incident?> GetOpenIncidentForProviderAsync(Guid providerId, CancellationToken ct = default)
        => await _context.Incidents
            .FirstOrDefaultAsync(i => i.ProviderId == providerId && i.ResolvedAt == null, ct);

    public async Task AddAsync(Incident incident, CancellationToken ct = default)
        => await _context.Incidents.AddAsync(incident, ct);
}