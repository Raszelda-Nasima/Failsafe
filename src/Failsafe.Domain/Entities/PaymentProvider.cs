using Failsafe.Domain.Enums;

namespace Failsafe.Domain.Entities;

// Represents a payment provider integration. Notice there is NO Status
// property here — status is a derived, real-time calculation (see
// ProviderHealthEvaluator), not a persisted field that could go stale.
public class PaymentProvider
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public ProviderType ProviderType { get; private set; }
    public int Priority { get; private set; }
    public int CostPerTransactionCents { get; private set; }
    public bool Enabled { get; private set; }

    // Audit trail: who registered this provider. Captured from the
    // authenticated Admin's JWT claims at registration time — a genuine
    // human action, unlike Incident creation, which is fully automated.
    public string CreatedByUserId { get; private set; } = default!;
    public string CreatedByName { get; private set; } = default!;

    private PaymentProvider() { } // required by EF Core

    // createdByUserId/createdByName default to "system"/"System" so
    // existing test call sites (which don't care about audit data) keep
    // compiling unchanged — real registrations always pass explicit values.
    public static PaymentProvider Register(
        string name, ProviderType providerType, int priority, int costPerTransactionCents,
        string createdByUserId = "system", string createdByName = "System")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A payment provider must have a name.", nameof(name));

        return new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProviderType = providerType,
            Priority = priority,
            CostPerTransactionCents = costPerTransactionCents,
            Enabled = true,
            CreatedByUserId = createdByUserId,
            CreatedByName = createdByName
        };
    }

    public void ChangePriority(int priority) => Priority = priority;

    public void ChangeCost(int costPerTransactionCents)
    {
        if (costPerTransactionCents < 0)
            throw new ArgumentException("Cost cannot be negative.", nameof(costPerTransactionCents));

        CostPerTransactionCents = costPerTransactionCents;
    }

    public void Disable() => Enabled = false;
    public void Enable() => Enabled = true;
}