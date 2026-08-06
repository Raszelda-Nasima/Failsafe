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

    // Lower number = tried first during failover. An int, not an enum,
    // since routing order needs to be freely reorderable by an Admin,
    // not constrained to a small fixed set of named positions.
    public int Priority { get; private set; }

    public int CostPerTransactionCents { get; private set; }

    // Allows an Admin to take a provider out of the routing pool entirely
    // (e.g. for planned maintenance) without deleting its historical data.
    public bool Enabled { get; private set; }

    private PaymentProvider() { } // required by EF Core

    public static PaymentProvider Register(
        string name, ProviderType providerType, int priority, int costPerTransactionCents)
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
            Enabled = true
        };
    }

    // Each independent concern gets its own method, same discipline as
    // Service.ChangeOwnerTeam in OpsLens — no single invariant ties
    // Priority and CostPerTransactionCents together, so they're not
    // bundled into one "UpdateDetails" call.
    public void ChangePriority(int priority) => Priority = priority;

    public void Disable() => Enabled = false;
    public void Enable() => Enabled = true;
}