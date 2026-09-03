using Failsafe.Application.Interfaces;
using Failsafe.Application.Providers;
using Failsafe.Domain.Entities;
using Failsafe.Domain.Enums;
using Failsafe.Domain.Services;
using Xunit;

namespace Failsafe.UnitTests.Application;

// Hand-written fakes rather than a mocking library — simple enough here
// that a library would add ceremony without real benefit at this scale.
internal class FakeProviderRepository : IPaymentProviderRepository
{
    private readonly List<PaymentProvider> _providers;
    public FakeProviderRepository(List<PaymentProvider> providers) => _providers = providers;

    public Task<PaymentProvider?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_providers.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<PaymentProvider>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PaymentProvider>>(_providers);

    public Task<IReadOnlyList<PaymentProvider>> GetEnabledOrderedByPriorityAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PaymentProvider>>(
            _providers.Where(p => p.Enabled).OrderBy(p => p.Priority).ToList());

    public Task AddAsync(PaymentProvider provider, CancellationToken ct = default)
    {
        _providers.Add(provider);
        return Task.CompletedTask;
    }
}

internal class FakeHealthCheckResultRepository : IHealthCheckResultRepository
{
    // Maps a provider id directly to the status its fake health checks
    // should evaluate to, letting each test set up exactly the scenario
    // it needs without generating realistic-looking check history.
    private readonly Dictionary<Guid, List<HealthCheckResult>> _resultsByProvider;
    public FakeHealthCheckResultRepository(Dictionary<Guid, List<HealthCheckResult>> resultsByProvider)
        => _resultsByProvider = resultsByProvider;

    public Task AddAsync(HealthCheckResult result, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<HealthCheckResult>> GetRecentByProviderIdAsync(
        Guid providerId, int count, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<HealthCheckResult>>(
            _resultsByProvider.GetValueOrDefault(providerId, new List<HealthCheckResult>()));
}

public class FailoverServiceTests
{
    // Builds a HealthCheckResult list that will evaluate to a specific
    // status via ProviderHealthEvaluator's real thresholds (5% / 20%),
    // so tests exercise the real evaluation logic, not a stubbed shortcut.
    private static List<HealthCheckResult> AllSuccessful(Guid providerId, int count = 20)
        => Enumerable.Range(0, count).Select(_ => HealthCheckResult.Record(providerId, 50, true)).ToList();

    private static List<HealthCheckResult> AllFailed(Guid providerId, int count = 20)
        => Enumerable.Range(0, count).Select(_ => HealthCheckResult.Record(providerId, 3000, false)).ToList();

    [Fact]
    public async Task SelectActiveProviderAsync_NeverReturnsProviderFromDifferentNetwork()
    {
        // A Visa provider and a Mastercard provider both healthy — a Visa
        // request must only ever be able to select the Visa provider.
        var visa = PaymentProvider.Register("Visa Primary", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var mastercard = PaymentProvider.Register("Mastercard Primary", ProviderType.Mastercard, priority: 1, costPerTransactionCents: 10);

        var providerRepo = new FakeProviderRepository(new List<PaymentProvider> { visa, mastercard });
        var healthRepo = new FakeHealthCheckResultRepository(new Dictionary<Guid, List<HealthCheckResult>>
        {
            [visa.Id] = AllSuccessful(visa.Id),
            [mastercard.Id] = AllSuccessful(mastercard.Id)
        });

        var service = new FailoverService(providerRepo, healthRepo, new ProviderHealthEvaluator(), new FailoverSelector());

        var result = await service.SelectActiveProviderAsync(ProviderType.Visa);

        Assert.NotNull(result);
        Assert.Equal(visa.Id, result!.Id);
    }

    [Fact]
    public async Task SelectActiveProviderAsync_ReturnsNull_WhenOnlyOtherNetworkProvidersAreHealthy()
    {
        // Visa is Offline, but PayPal is Healthy — a Visa request must
        // still return null rather than incorrectly substituting PayPal.
        var visa = PaymentProvider.Register("Visa Primary", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);
        var paypal = PaymentProvider.Register("PayPal", ProviderType.PayPal, priority: 1, costPerTransactionCents: 10);

        var providerRepo = new FakeProviderRepository(new List<PaymentProvider> { visa, paypal });
        var healthRepo = new FakeHealthCheckResultRepository(new Dictionary<Guid, List<HealthCheckResult>>
        {
            [visa.Id] = AllFailed(visa.Id),
            [paypal.Id] = AllSuccessful(paypal.Id)
        });

        var service = new FailoverService(providerRepo, healthRepo, new ProviderHealthEvaluator(), new FailoverSelector());

        var result = await service.SelectActiveProviderAsync(ProviderType.Visa);

        Assert.Null(result);
    }

    [Fact]
    public async Task SelectActiveProviderAsync_ReturnsProvider_WhenOnlyASingleProviderExistsAndIsHealthy()
    {
        // A single configured provider for a network is a real, valid
        // scenario — it just means there's no redundancy if it fails.
        // This test documents that as expected behavior, not a bug.
        var visa = PaymentProvider.Register("Visa Only", ProviderType.Visa, priority: 1, costPerTransactionCents: 10);

        var providerRepo = new FakeProviderRepository(new List<PaymentProvider> { visa });
        var healthRepo = new FakeHealthCheckResultRepository(new Dictionary<Guid, List<HealthCheckResult>>
        {
            [visa.Id] = AllSuccessful(visa.Id)
        });

        var service = new FailoverService(providerRepo, healthRepo, new ProviderHealthEvaluator(), new FailoverSelector());

        var result = await service.SelectActiveProviderAsync(ProviderType.Visa);

        Assert.NotNull(result);
        Assert.Equal(visa.Id, result!.Id);
    }
}