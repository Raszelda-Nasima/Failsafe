namespace Failsafe.Application.Providers.DTOs;

// What the API returns. Status is included here even though it's NOT a
// stored field on PaymentProvider — it's computed by ProviderHealthEvaluator
// at the moment this response is built, so the client sees a live value.
public record ProviderResponse(
    Guid Id,
    string Name,
    string ProviderType,
    int Priority,
    int CostPerTransactionCents,
    bool Enabled,
    string Status
);