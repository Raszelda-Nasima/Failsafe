namespace Failsafe.Domain.Entities;

// An immutable historical fact, same category as OpsLens's Deployment
// entity — it happened, it's recorded, it never changes afterward.
// No state machine, no update methods, deliberately.
public class HealthCheckResult
{
    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public DateTime CheckedAt { get; private set; }
    public int ResponseTimeMs { get; private set; }
    public bool IsSuccessful { get; private set; }

    private HealthCheckResult() { }

    public static HealthCheckResult Record(Guid providerId, int responseTimeMs, bool isSuccessful)
        => new()
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            CheckedAt = DateTime.UtcNow,
            ResponseTimeMs = responseTimeMs,
            IsSuccessful = isSuccessful
        };
}