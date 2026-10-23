using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache.Tests;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddMitMediatorInMemoryCache_RepeatedCalls_ShouldLeaveServicesAndConfigurationUnchanged(bool differentArguments)
    {
        var services = new ServiceCollection();
        var options = new MemoryCacheOptions
        {
            Clock = new TestClock(),
            SizeLimit = 123,
            CompactionPercentage = 0.4,
            ExpirationScanFrequency = TimeSpan.FromSeconds(17),
            TrackLinkedCacheEntries = true,
            TrackStatistics = true
        };
        var assemblies = new[] { typeof(DependencyInjectionTests).Assembly };
        services.AddMitMediatorInMemoryCache(options, lockStripingGatesCount: 32, assemblies: assemblies);
        var descriptors = services.ToArray();

        var result = differentArguments
            ? services.AddMitMediatorInMemoryCache(
                new MemoryCacheOptions { SizeLimit = 999 },
                lockStripingGatesCount: 128,
                assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly])
            : services.AddMitMediatorInMemoryCache(options, lockStripingGatesCount: 32, assemblies: assemblies);

        Assert.Same(services, result);
        Assert.Equal(descriptors, services.ToArray());
        using var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetRequiredService<IOptions<MemoryCacheOptions>>().Value;
        Assert.Same(options.Clock, registeredOptions.Clock);
        Assert.Equal(options.SizeLimit, registeredOptions.SizeLimit);
        Assert.Equal(options.CompactionPercentage, registeredOptions.CompactionPercentage);
        Assert.Equal(options.ExpirationScanFrequency, registeredOptions.ExpirationScanFrequency);
        Assert.Equal(options.TrackLinkedCacheEntries, registeredOptions.TrackLinkedCacheEntries);
        Assert.Equal(options.TrackStatistics, registeredOptions.TrackStatistics);
        Assert.Equal(32,
            provider.GetRequiredService<IOptions<CacheRequestTypeLocksStripingOptions>>().Value.LockStripingGatesCount);
        Assert.Contains(typeof(FirstCachedRequest),
            provider.GetRequiredService<ClearCacheMatrix>().ClearCacheMatrixDictionary[typeof(TriggerRequest)]);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_RepeatedCall_ShouldPreserveDefaultOptionsAndEmptyMatrix()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        var descriptors = services.ToArray();

        services.AddMitMediatorInMemoryCache(
            new MemoryCacheOptions { SizeLimit = 123 },
            lockStripingGatesCount: 0,
            assemblies: [typeof(DependencyInjectionTests).Assembly]);

        Assert.Equal(descriptors, services.ToArray());
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetRequiredService<IOptions<MemoryCacheOptions>>().Value.SizeLimit);
        Assert.Equal(64,
            provider.GetRequiredService<IOptions<CacheRequestTypeLocksStripingOptions>>().Value.LockStripingGatesCount);
        Assert.Empty(provider.GetRequiredService<ClearCacheMatrix>().ClearCacheMatrixDictionary);
    }

    [Fact]
    public async Task RegisteredPipeline_RepeatedRegistration_ShouldCacheAndInvalidateWithoutDeadlock()
    {
        var services = new ServiceCollection();
        services.AddMitMediator(typeof(IMediator).Assembly);
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(DependencyInjectionTests).Assembly]);
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(DependencyInjectionTests).Assembly]);
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        var queryCalls = 0;
        var triggerCalls = 0;
        services.AddSingleton<IRequestHandler<FirstCachedRequest, string>>(
            new Handler<FirstCachedRequest>((_, _) => ValueTask.FromResult($"response {++queryCalls}")));
        services.AddSingleton<IRequestHandler<UncachedRequest, string>>(
            new Handler<UncachedRequest>((_, _) =>
            {
                triggerCalls++;
                return ValueTask.FromResult("updated");
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var source = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new FirstCachedRequest();

        Assert.Collection(scope.ServiceProvider.GetServices<IPipelineBehavior<FirstCachedRequest, string>>(),
            behavior => Assert.IsType<InMemoryClearCacheByMatrixBehavior<FirstCachedRequest, string>>(behavior),
            behavior => Assert.IsType<InMemoryCacheBehavior<FirstCachedRequest, string>>(behavior));
        Assert.Single(scope.ServiceProvider.GetServices<INotificationHandler<ClearAllResponsesCacheNotification>>());
        Assert.Single(scope.ServiceProvider.GetServices<INotificationHandler<ClearResponseCacheForRequestNotification>>());

        Assert.Equal("response 1", await mediator.SendAsync<FirstCachedRequest, string>(query, source.Token));
        Assert.Equal("response 1", await mediator.SendAsync<FirstCachedRequest, string>(query, source.Token));
        Assert.Equal(1, queryCalls);

        Assert.Equal("updated", await mediator.SendAsync<UncachedRequest, string>(new UncachedRequest(), source.Token));
        Assert.Equal(1, triggerCalls);
        Assert.Equal("response 2", await mediator.SendAsync<FirstCachedRequest, string>(query, source.Token));

        await mediator.ClearResponseCacheAsync(query, source.Token);
        Assert.Equal("response 3", await mediator.SendAsync<FirstCachedRequest, string>(query, source.Token));

        await mediator.ClearAllResponseCacheAsync<FirstCachedRequest>(source.Token);
        Assert.Equal("response 4", await mediator.SendAsync<FirstCachedRequest, string>(query, source.Token));
        Assert.Equal(4, queryCalls);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void AddMitMediatorInMemoryCache_ShouldConfigureLocksStriping(int gatesCount)
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(
            lockStripingGatesCount: gatesCount,
            assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal(gatesCount,
            provider.GetRequiredService<IOptions<CacheRequestTypeLocksStripingOptions>>().Value.LockStripingGatesCount);
        var locks = provider.GetRequiredService<CacheRequestTypeLocksStriping<UncachedRequest>>();
        Assert.Same(locks, scope.ServiceProvider.GetRequiredService<CacheRequestTypeLocksStriping<UncachedRequest>>());
        Assert.NotSame((object)locks, provider.GetRequiredService<CacheRequestTypeLocksStriping<TriggerRequest>>());

        var gatesField = typeof(CacheRequestTypeLocksStriping<UncachedRequest>)
            .GetField("_gates", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var gates = Assert.IsType<SemaphoreSlim[]>(gatesField.GetValue(locks));
        Assert.Equal(gatesCount, gates.Length);
        Assert.All(gates, gate => Assert.Equal(1, gate.CurrentCount));
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_ShouldConfigureDefaultLocksStriping()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(64,
            provider.GetRequiredService<IOptions<CacheRequestTypeLocksStripingOptions>>().Value.LockStripingGatesCount);
        Assert.NotNull(provider.GetRequiredService<CacheRequestTypeLocksStriping<UncachedRequest>>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddMitMediatorInMemoryCache_InvalidGatesCount_ShouldRejectLocksStriping(int gatesCount)
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(
            lockStripingGatesCount: gatesCount,
            assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<CacheRequestTypeLocksStriping<UncachedRequest>>());
    }

    [Fact]
    public void AddRequestsInMemoryCache_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache();
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<MemoryCache>());
    }

    [Fact]
    public void AddRequestsInMemoryCache_WithOptions_ShouldRegisterWithOptions()
    {
        var services = new ServiceCollection();
        var options = new MemoryCacheOptions { SizeLimit = 123 };
        services.AddMitMediatorInMemoryCache(options);
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetService<MemoryCache>()!;
        Assert.NotNull(cache);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_ShouldCopyAllSupportedOptions()
    {
        var services = new ServiceCollection();
        var options = new MemoryCacheOptions
        {
            Clock = new TestClock(),
            SizeLimit = 123,
            CompactionPercentage = 0.4,
            ExpirationScanFrequency = TimeSpan.FromSeconds(17),
            TrackLinkedCacheEntries = true,
            TrackStatistics = true
        };

        Assert.Same(services, services.AddMitMediatorInMemoryCache(options, assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]));
        using var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetRequiredService<IOptions<MemoryCacheOptions>>().Value;

        Assert.Same(options.Clock, registeredOptions.Clock);
        Assert.Equal(options.SizeLimit, registeredOptions.SizeLimit);
        Assert.Equal(options.CompactionPercentage, registeredOptions.CompactionPercentage);
        Assert.Equal(options.ExpirationScanFrequency, registeredOptions.ExpirationScanFrequency);
        Assert.Equal(options.TrackLinkedCacheEntries, registeredOptions.TrackLinkedCacheEntries);
        Assert.Equal(options.TrackStatistics, registeredOptions.TrackStatistics);
        Assert.NotNull(provider.GetRequiredService<IMemoryCache>());
        Assert.Same(provider.GetRequiredService<MemoryCache>(), provider.GetRequiredService<MemoryCache>());
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_ShouldRegisterBehaviorAndNotificationHandlers()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Collection(scope.ServiceProvider.GetServices<IPipelineBehavior<UncachedRequest, string>>(),
            behavior => Assert.IsType<InMemoryClearCacheByMatrixBehavior<UncachedRequest, string>>(behavior),
            behavior => Assert.IsType<InMemoryCacheBehavior<UncachedRequest, string>>(behavior));
        Assert.IsType<ClearAllResponsesCacheNotificationHandler>(
            scope.ServiceProvider.GetRequiredService<INotificationHandler<ClearAllResponsesCacheNotification>>());
        Assert.IsType<ClearResponseCacheForRequestHandler>(
            scope.ServiceProvider.GetRequiredService<INotificationHandler<ClearResponseCacheForRequestNotification>>());
        Assert.Collection(services.Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>)),
            descriptor =>
            {
                Assert.Equal(typeof(InMemoryClearCacheByMatrixBehavior<,>), descriptor.ImplementationType);
                Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
            },
            descriptor =>
            {
                Assert.Equal(typeof(InMemoryCacheBehavior<,>), descriptor.ImplementationType);
                Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
            });
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INotificationHandler<ClearAllResponsesCacheNotification>) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INotificationHandler<ClearResponseCacheForRequestNotification>) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_NullAssemblies_ShouldScanLoadedAssemblies()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(assemblies: null);
        using var provider = services.BuildServiceProvider();
        var matrix = provider.GetRequiredService<ClearCacheMatrix>();

        Assert.Contains(typeof(FirstCachedRequest), matrix.ClearCacheMatrixDictionary[typeof(TriggerRequest)]);
        Assert.Contains(typeof(SecondCachedRequest), matrix.ClearCacheMatrixDictionary[typeof(TriggerRequest)]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RegisteredPipeline_ShouldCacheQueriesAndInvalidateOnTriggers(bool cachedTrigger, bool cacheHit)
    {
        var services = new ServiceCollection();
        services.AddMitMediator(typeof(IMediator).Assembly);
        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(DependencyInjectionTests).Assembly]);
        var queryCalls = 0;
        var triggerCalls = 0;
        services.AddSingleton<IRequestHandler<FirstCachedRequest, string>>(
            new Handler<FirstCachedRequest>((_, _) => ValueTask.FromResult($"response {++queryCalls}")));
        services.AddSingleton<IRequestHandler<CachedWithoutTriggers, string>>(
            new Handler<CachedWithoutTriggers>((_, _) =>
            {
                triggerCalls++;
                return ValueTask.FromResult("updated");
            }));
        services.AddSingleton<IRequestHandler<UncachedRequest, string>>(
            new Handler<UncachedRequest>((_, _) =>
            {
                triggerCalls++;
                return ValueTask.FromResult("updated");
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var query = new FirstCachedRequest();
        if (cacheHit)
        {
            cache.Set(new CachedWithoutTriggers().GetCacheEntryKey(), "cached trigger");
        }

        Assert.Equal("response 1", await mediator.SendAsync<FirstCachedRequest, string>(query, CancellationToken.None));
        Assert.Equal("response 1", await mediator.SendAsync<FirstCachedRequest, string>(query, CancellationToken.None));
        Assert.Equal(1, queryCalls);

        var result = cachedTrigger
            ? await mediator.SendAsync<CachedWithoutTriggers, string>(new CachedWithoutTriggers(), CancellationToken.None)
            : await mediator.SendAsync<UncachedRequest, string>(new UncachedRequest(), CancellationToken.None);

        Assert.Equal(cacheHit ? "cached trigger" : "updated", result);
        Assert.Equal(cacheHit ? 0 : 1, triggerCalls);
        Assert.False(cache.TryGetValue(query.GetCacheEntryKey(), out _));
        Assert.Equal("response 2", await mediator.SendAsync<FirstCachedRequest, string>(query, CancellationToken.None));
        Assert.Equal(2, queryCalls);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_ExplicitAssembly_ShouldAggregateTriggersAndSkipAbstractTypes()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(DependencyInjectionTests).Assembly]);
        using var provider = services.BuildServiceProvider();
        var matrix = provider.GetRequiredService<ClearCacheMatrix>();

        var targets = matrix.ClearCacheMatrixDictionary[typeof(TriggerRequest)];
        Assert.Equal(2, targets.Length);
        Assert.Contains(typeof(FirstCachedRequest), targets);
        Assert.Contains(typeof(SecondCachedRequest), targets);
        Assert.Equal(new[] { typeof(FirstCachedRequest) },
            matrix.ClearCacheMatrixDictionary[typeof(OtherTriggerRequest)]);
        Assert.DoesNotContain(typeof(AbstractCachedRequest), targets);
        Assert.DoesNotContain(typeof(UncachedRequest), targets);
        Assert.DoesNotContain(typeof(CachedWithoutTriggers), targets);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_AssemblyWithoutTriggers_ShouldRegisterEmptyMatrix()
    {
        var services = new ServiceCollection();

        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(CacheResponseInMemoryAttribute).Assembly]);
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<ClearCacheMatrix>().ClearCacheMatrixDictionary);
    }

    [Fact]
    public void AddMitMediatorInMemoryCache_ShouldRegisterMatrixAsSingletonPerProvider()
    {
        var services = new ServiceCollection();
        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(DependencyInjectionTests).Assembly]);
        using var firstProvider = services.BuildServiceProvider();
        using var secondProvider = services.BuildServiceProvider();
        using var scope = firstProvider.CreateScope();
        var matrix = firstProvider.GetRequiredService<ClearCacheMatrix>();
        var invalidation = firstProvider.GetRequiredService<CacheInvalidationService>();

        Assert.Same(matrix, scope.ServiceProvider.GetRequiredService<ClearCacheMatrix>());
        Assert.NotSame(matrix, secondProvider.GetRequiredService<ClearCacheMatrix>());
        Assert.Same(invalidation, scope.ServiceProvider.GetRequiredService<CacheInvalidationService>());
        Assert.NotSame(invalidation, secondProvider.GetRequiredService<CacheInvalidationService>());
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(CacheInvalidationService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ClearCacheMatrix) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredPipeline_AnotherContainer_ShouldNotChangeInvalidationRules(bool otherHasRules)
    {
        var services = new ServiceCollection();
        services.AddMitMediator(typeof(IMediator).Assembly);
        services.AddMitMediatorInMemoryCache(null, assemblies: [typeof(DependencyInjectionTests).Assembly]);
        services.AddSingleton<IRequestHandler<FirstCachedRequest, string>>(
            new Handler<FirstCachedRequest>((_, _) => ValueTask.FromResult("query")));
        services.AddSingleton<IRequestHandler<UncachedRequest, string>>(
            new Handler<UncachedRequest>((_, _) => ValueTask.FromResult("updated")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var query = new FirstCachedRequest();
        await mediator.SendAsync<FirstCachedRequest, string>(query, CancellationToken.None);
        Assert.True(cache.TryGetValue(query.GetCacheEntryKey(), out _));

        Assembly otherAssembly = typeof(CacheResponseInMemoryAttribute).Assembly;
        Type? otherQuery = null;
        if (otherHasRules)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"OtherCacheRules_{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
            var type = assembly.DefineDynamicModule("Rules").DefineType("OtherCachedRequest", TypeAttributes.Public);
            type.AddInterfaceImplementation(typeof(IRequest<string>));
            var constructor = typeof(CacheResponseInMemoryAttribute).GetConstructor([typeof(Type[])])!;
            type.SetCustomAttribute(new CustomAttributeBuilder(constructor, [new[] { typeof(UncachedRequest) }]));
            otherQuery = type.CreateType()!;
            otherAssembly = assembly;
        }

        var otherServices = new ServiceCollection();
        otherServices.AddMitMediatorInMemoryCache(null, assemblies: [otherAssembly]);
        using var otherProvider = otherServices.BuildServiceProvider();
        var otherMatrix = otherProvider.GetRequiredService<ClearCacheMatrix>().ClearCacheMatrixDictionary;
        if (otherHasRules)
        {
            Assert.Equal(new[] { otherQuery! }, otherMatrix[typeof(UncachedRequest)]);
        }
        else
        {
            Assert.Empty(otherMatrix);
        }

        await mediator.SendAsync<UncachedRequest, string>(new UncachedRequest(), CancellationToken.None);

        Assert.False(cache.TryGetValue(query.GetCacheEntryKey(), out _));
        Assert.Contains(typeof(FirstCachedRequest),
            provider.GetRequiredService<ClearCacheMatrix>().ClearCacheMatrixDictionary[typeof(UncachedRequest)]);
    }

    private sealed class TestClock : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private class TriggerRequest : IRequest<string>;
    private class OtherTriggerRequest : IRequest<string>;
    private class UncachedRequest : IRequest<string>;

    [CacheResponseInMemory]
    private class CachedWithoutTriggers : IRequest<string>;

    [CacheResponseInMemory(typeof(TriggerRequest), typeof(OtherTriggerRequest),
        typeof(UncachedRequest), typeof(CachedWithoutTriggers))]
    private class FirstCachedRequest : IRequest<string>;

    [CacheResponseInMemory(typeof(TriggerRequest))]
    private class SecondCachedRequest : IRequest<string>;

    [CacheResponseInMemory(typeof(TriggerRequest))]
    private abstract class AbstractCachedRequest : IRequest<string>;

    private sealed class Handler<TRequest>(Func<TRequest, CancellationToken, ValueTask<string>> invoke)
        : IRequestHandler<TRequest, string>
        where TRequest : IRequest<string>
    {
        public ValueTask<string> HandleAsync(TRequest request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }
}