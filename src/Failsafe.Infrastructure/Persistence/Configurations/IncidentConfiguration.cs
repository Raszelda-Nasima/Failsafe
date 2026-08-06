using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Failsafe.Domain.Entities;

namespace Failsafe.Infrastructure.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Reason).IsRequired().HasMaxLength(500);

        builder.HasOne<PaymentProvider>()
            .WithMany()
            .HasForeignKey(i => i.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Duration is a computed C# property, not a real column — tell
        // EF Core to ignore it, or it will try (and fail) to map it.
        builder.Ignore(i => i.Duration);
    }
}