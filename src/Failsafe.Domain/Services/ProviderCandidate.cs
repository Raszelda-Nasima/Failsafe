using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;

namespace Failsafe.Domain.Services;

// Pairs a provider with its live computed status. This pairing itself
// requires health check data (I/O), so it's assembled in the Application
// layer — but the type describing the pairing belongs in Domain, since
// it's what FailoverSelector's pure selection logic actually reasons over.
public record ProviderCandidate(PaymentProvider Provider, ProviderStatus Status);
