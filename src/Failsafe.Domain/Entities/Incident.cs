namespace Failsafe.Domain.Entities;

// Simpler state machine than OpsLens's Incident — this one is entirely
// system-managed (auto-created on failure-rate breach, auto-resolved on
// recovery), so there's no multi-step human workflow to model, just an
// open/closed lifecycle with a duration that directly feeds the MTTR metric.
public class Incident
{
    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string Reason { get; private set; } = default!;

    private Incident() { }

    public static Incident Open(Guid providerId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An incident must have a reason.", nameof(reason));

        return new Incident
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            Reason = reason,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Resolve()
    {
        if (ResolvedAt is not null)
            throw new InvalidOperationException("This incident is already resolved.");

        ResolvedAt = DateTime.UtcNow;
    }

    // A computed property, not a stored one — derived directly from the
    // two dates above, so it can never be set incorrectly or forgotten.
    public TimeSpan? Duration => ResolvedAt.HasValue ? ResolvedAt.Value - StartedAt : null;
}