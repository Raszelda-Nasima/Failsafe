using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Failsafe.Domain.Entities;

namespace Failsafe.Infrastructure.Persistence.Configurations;

public class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.ToTable("PaymentProviders");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => p.Name).IsUnique();

        // Readable string in the DB, not a magic number.
        builder.Property(p => p.ProviderType).HasConversion<string>().HasMaxLength(20);
    }
}