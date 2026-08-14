using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;
using Xunit;

namespace Failsafe.UnitTests.Domain;

// Tests FailoverSelector in complete isolation — no database, no
// repositories, no mocking framework needed. Every candidate is a plain
// in-memory object, which is exactly the payoff of keeping this logic in
// Domain rather than mixed into a use-case class that would require
// mocking IPaymentProviderRepository/IHealthCheckResultRepository just to
// test a simple selection rule.
public class FailoverSelectorTests
{
    private readonly FailoverSelector _selector = new();

    [Fact]
    public void SelectProvider_ReturnsHighestPriorityHealthyProvider_WhenOneExists()
    {
        // Arrange: two candidates, Visa (priority 1, Warning) and
        // Mastercard (priority 2, Healthy). Even though Visa is
        // higher-priority overall, only Healthy providers are eligible
        // for this first tier of selection.
        var visa = PaymentProvider.Register("Visa", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var mastercard = PaymentProvider.Register("Mastercard", ProviderType.Mastercard, priority: 2, costPerTransactionCents: 12);

        var candidates = new List<ProviderCandidate>
        {
            new(visa, ProviderStatus.Warning),
            new(mastercard, ProviderStatus.Healthy)
        };

        // Act
        var result = _selector.SelectProvider(candidates);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(mastercard.Id, result!.Id);
    }

    [Fact]
    public void SelectProvider_PrefersHigherPriorityAmongMultipleHealthyProviders()
    {
        // Arrange: both Healthy — priority must be the deciding factor.
        var visa = PaymentProvider.Register("Visa", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var mastercard = PaymentProvider.Register("Mastercard", ProviderType.Mastercard, priority: 2, costPerTransactionCents: 12);

        // Deliberately listed out of priority order, to prove the selector
        // itself does the right thing rather than relying on input order.
        var candidates = new List<ProviderCandidate>
        {
            new(mastercard, ProviderStatus.Healthy),
            new(visa, ProviderStatus.Healthy)
        };

        var result = _selector.SelectProvider(candidates);

        Assert.NotNull(result);
        Assert.Equal(visa.Id, result!.Id);
    }

    [Fact]
    public void SelectProvider_FallsBackToWarningProvider_WhenNoHealthyProviderExists()
    {
        // Arrange: nothing Healthy, but one Warning provider — should
        // still be selected rather than returning null outright.
        var visa = PaymentProvider.Register("Visa", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var mastercard = PaymentProvider.Register("Mastercard", ProviderType.Mastercard, priority: 2, costPerTransactionCents: 12);

        var candidates = new List<ProviderCandidate>
        {
            new(visa, ProviderStatus.Offline),
            new(mastercard, ProviderStatus.Warning)
        };

        var result = _selector.SelectProvider(candidates);

        Assert.NotNull(result);
        Assert.Equal(mastercard.Id, result!.Id);
    }

    [Fact]
    public void SelectProvider_ReturnsNull_WhenEveryProviderIsOffline()
    {
        // Arrange: no viable candidate at all — the genuine "nobody to
        // route to" case the controller needs to handle explicitly.
        var visa = PaymentProvider.Register("Visa", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var mastercard = PaymentProvider.Register("Mastercard", ProviderType.Mastercard, priority: 2, costPerTransactionCents: 12);

        var candidates = new List<ProviderCandidate>
        {
            new(visa, ProviderStatus.Offline),
            new(mastercard, ProviderStatus.Offline)
        };

        var result = _selector.SelectProvider(candidates);

        Assert.Null(result);
    }

    [Fact]
    public void SelectProvider_ReturnsNull_WhenCandidateListIsEmpty()
    {
        // Arrange: no enabled providers at all (e.g. everything disabled,
        // or none registered yet) — a valid, real edge case, not just a
        // theoretical one, especially right after a fresh deployment.
        var candidates = new List<ProviderCandidate>();

        var result = _selector.SelectProvider(candidates);

        Assert.Null(result);
    }
}