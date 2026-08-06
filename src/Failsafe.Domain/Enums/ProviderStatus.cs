namespace Failsafe.Domain.Enums;

// Deliberately NOT stored on PaymentProvider itself — this is always
// computed live from recent HealthCheckResult rows via
// ProviderHealthEvaluator, so it can never drift out of sync with the
// actual underlying data.
public enum ProviderStatus
{
    Healthy,
    Warning,
    Offline
}