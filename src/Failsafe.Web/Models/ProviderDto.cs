namespace Failsafe.Web.Models;

// Mirrors OpsLens... err, Failsafe.Application.Providers.DTOs.ProviderResponse
// exactly. Kept as a separate definition here since Failsafe.Web has no
// project reference to the API — it only ever talks to it over HTTP, same
// arm's-length separation as any external API consumer would have.
public record ProviderDto(
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