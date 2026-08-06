using Failsafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Failsafe.Infrastructure.Persistence;

public class FailsafeDbContext : DbContext
{
    public FailsafeDbContext(DbContextOptions<FailsafeDbContext> options) : base(options) { }

    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<HealthCheckResult> HealthCheckResults => Set<HealthCheckResult>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-discovers every IEntityTypeConfiguration<T> in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FailsafeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}