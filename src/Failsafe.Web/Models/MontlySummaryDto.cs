namespace Failsafe.Web.Models;

// Mirrors IncidentsController.GetMonthlySummary's response shape exactly.
public record MonthlySummaryDto(
    string Month,
    int TotalIncidents,
    int OpenIncidents,
    int ResolvedIncidents,
    int ResolutionRatePercent,
    double TotalDowntimeMinutes,
    double? AverageResolutionMinutes,
    double AverageCostPerTransactionCents,
    List<IncidentByProviderDto> IncidentsByProvider,
    ProviderHealthBreakdownDto ProviderHealthBreakdown,
    List<CostByProviderDto> CostByProvider
);

public record IncidentByProviderDto(string Provider, int Count);
public record ProviderHealthBreakdownDto(int Healthy, int Warning, int Offline);
public record CostByProviderDto(string Name, int CostPerTransactionCents);