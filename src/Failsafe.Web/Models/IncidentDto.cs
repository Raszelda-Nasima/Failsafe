namespace Failsafe.Web.Models;

// Mirrors IncidentsController.GetAll's anonymous response shape.
public record IncidentDto(
    Guid Id,
    Guid ProviderId,
    string Reason,
    DateTime StartedAt,
    DateTime? ResolvedAt,
    bool IsOpen,
    double? DurationSeconds
);