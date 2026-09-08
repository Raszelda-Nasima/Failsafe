namespace Failsafe.Application.Providers.DTOs;

// What the API returns. New fields appended at the end so any existing
// positional call sites don't break.
public record ProviderResponse(
    Guid Id,
    string Name,
    string ProviderType,
    int Priority,
    int CostPerTransactionCents,
    bool Enabled,
    string Status,
    string CreatedByName,
    int HealthScore,
    double UptimePercent,
    int RecentIncidentCount
);