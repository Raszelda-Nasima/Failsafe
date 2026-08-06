namespace Failsafe.Application.Providers.DTOs;

// Raw shape crossing the HTTP boundary — never the Domain entity itself,
// same seam discipline as OpsLens's CreateServiceRequest.
public record CreateProviderRequest(
    string Name,
    string ProviderType,
    int Priority,
    int CostPerTransactionCents
);