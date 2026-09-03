namespace Failsafe.Application.Providers.DTOs;

// Deliberately excludes Name and ProviderType — see the controller
// comment for why these are treated as immutable after registration.
public record UpdateProviderRequest(int Priority, int CostPerTransactionCents, bool Enabled);