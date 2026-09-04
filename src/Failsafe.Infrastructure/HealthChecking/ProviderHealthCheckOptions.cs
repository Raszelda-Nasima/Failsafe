namespace Failsafe.Infrastructure.HealthChecking;

// Plain options class bound from appsettings.json's "ProviderHealthCheck"
// section. Lives in Infrastructure, not Domain — Domain must never
// reference Microsoft.Extensions.Options, so ProviderHealthEvaluator
// still takes plain constructor parameters; this class exists purely to
// carry configuration values from appsettings.json to the DI wiring in
// Program.cs, which then passes plain values into Domain constructors.
public class ProviderHealthCheckOptions
{
    public int IntervalSeconds { get; set; } = 15;
    public double WarningFailureRateThreshold { get; set; } = 0.05;
    public double OfflineFailureRateThreshold { get; set; } = 0.20;
    public double SimulatedBaselineFailureRate { get; set; } = 0.08;
}