namespace Failsafe.Web.Models;

// Mirrors PaymentProvidersController.GetActiveProvider's response shape.
public record RouteResultDto(
    Guid SelectedProviderId,
    string SelectedProviderName,
    string Network,
    List<CandidateDto> Candidates
);

public record CandidateDto(
    Guid Id,
    string Name,
    int Priority,
    string Status,
    bool Selected
);