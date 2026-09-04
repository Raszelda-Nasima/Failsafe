using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Failsafe.Infrastructure.HealthChecking;

// Exposes real business state as OpenTelemetry metrics, not just generic
// HTTP/runtime signals. ObservableGauge reads from this cache whenever
// Prometheus scrapes /metrics, rather than pushing a value synchronously —
// this is the standard pattern for metrics whose current value can simply
// be looked up rather than computed at emission time.
public static class ProviderMetrics
{
    private static readonly Meter Meter = new("Failsafe.Providers");

    // Provider name -> numeric status (0=Offline, 1=Warning, 2=Healthy).
    // A plain int, not the enum itself, because Prometheus metrics are
    // always numeric — Grafana can map these back to labels for display.
    private static readonly ConcurrentDictionary<string, int> CurrentStatus = new();

    private static readonly ConcurrentDictionary<string, int> _ = InitializeGauge();

    private static ConcurrentDictionary<string, int> InitializeGauge()
    {
        Meter.CreateObservableGauge(
            "failsafe_provider_status",
            () => CurrentStatus.Select(kvp =>
                new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>("provider", kvp.Key))),
            description: "Current computed status per provider: 0=Offline, 1=Warning, 2=Healthy");
        return CurrentStatus;
    }

    private static readonly Counter<int> IncidentsOpenedCounter =
        Meter.CreateCounter<int>("failsafe_incidents_opened_total", description: "Total incidents opened across all providers");

    // Called by ProviderHealthCheckService after each evaluation — updates
    // the cache the gauge reads from on the next scrape.
    public static void RecordStatus(string providerName, Domain.Enums.ProviderStatus status)
    {
        CurrentStatus[providerName] = status switch
        {
            Domain.Enums.ProviderStatus.Offline => 0,
            Domain.Enums.ProviderStatus.Warning => 1,
            Domain.Enums.ProviderStatus.Healthy => 2,
            _ => 0
        };
    }

    public static void RecordIncidentOpened() => IncidentsOpenedCounter.Add(1);
}
