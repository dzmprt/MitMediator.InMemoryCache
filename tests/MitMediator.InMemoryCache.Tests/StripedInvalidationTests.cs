using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache.Tests;

public class StripedInvalidationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DerivedRequest_Invalidation_ShouldWaitForBaseTypedWriter(bool singleEntry)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var request = new UnannotatedDerivedQuery();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var behavior = new InMemoryCacheBehavior<BaseQuery, string>(
            cache, services.Locks<BaseQuery>(), services.Invalidation);
        var writing = behavior.HandleAsync(request, new Next<BaseQuery>(async (_, _) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(Timeout);
            return "old";
        }), default).AsTask();
        await started.Task.WaitAsync(Timeout);
        var invalidation = singleEntry
            ? new ClearResponseCacheForRequestHandler(services.Invalidation)
                .HandleAsync(new ClearResponseCacheForRequestNotification(request), default).AsTask()
            : new ClearAllResponsesCacheNotificationHandler(services.Invalidation)
                .HandleAsync(new ClearAllResponsesCacheNotification(typeof(UnannotatedDerivedQuery)), default).AsTask();

        try
        {
            Assert.False(invalidation.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(writing, invalidation).WaitAsync(Timeout);
        }

        Assert.False(cache.TryGetValue(request.GetCacheEntryKey(), out _));
    }

    [Fact]
    public async Task DerivedRequest_BaseAndDerivedTypedWriters_ShouldShareLocks()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var request = new DerivedQuery();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var baseBehavior = new InMemoryCacheBehavior<BaseQuery, string>(
            cache, services.Locks<BaseQuery>(), services.Invalidation);
        var derivedBehavior = new InMemoryCacheBehavior<DerivedQuery, string>(
            cache, services.Locks<DerivedQuery>(), services.Invalidation);
        var first = baseBehavior.HandleAsync(request, new Next<BaseQuery>(async (_, _) =>
        {
            calls++;
            started.SetResult();
            await release.Task.WaitAsync(Timeout);
            return "first";
        }), default).AsTask();
        await started.Task.WaitAsync(Timeout);
        var second = derivedBehavior.HandleAsync(request, new Next<DerivedQuery>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult("second");
        }), default).AsTask();

        try
        {
            Assert.False(second.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(Timeout);
        }

        Assert.Equal("first", await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CachedTrigger_ShouldReleaseWriteStripeBeforeInvalidatingItsOwnType()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache, gatesCount: 1);
        var request = new CachedUpdate();
        var matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedUpdate)] = [typeof(CachedUpdate)]
        });
        var caching = new InMemoryCacheBehavior<CachedUpdate, string>(cache, services.Locks<CachedUpdate>(), services.Invalidation);
        var clearing = new InMemoryClearCacheByMatrixBehavior<CachedUpdate, string>(services.Invalidation, matrix);
        var result = await clearing.HandleAsync(request, new Next<CachedUpdate>((actual, token) =>
            caching.HandleAsync(actual,
                new Next<CachedUpdate>((_, _) => ValueTask.FromResult("updated")), token)), default)
            .AsTask().WaitAsync(Timeout);

        Assert.Equal("updated", result);
        Assert.False(cache.TryGetValue(request.GetCacheEntryKey(), out _));
        Assert.Equal(1, services.Locks<CachedUpdate>().ForRequest(request.GetCacheEntryKey()).CurrentCount);
    }

    [Theory]
    [InlineData(InvalidationKind.Trigger)]
    [InlineData(InvalidationKind.CachedTrigger)]
    [InlineData(InvalidationKind.Type)]
    [InlineData(InvalidationKind.Entry)]
    public async Task Invalidation_ShouldWaitForInFlightWriteThenRemoveIt(InvalidationKind kind)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var behavior = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation);
        var request = new Query { Id = 1 };
        var other = new Query { Id = 2 };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.Set(other.GetCacheEntryKey(), "other");
        var first = behavior.HandleAsync(request, new Next<Query>(async (_, _) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(Timeout);
            return "old";
        }), default).AsTask();
        await started.Task.WaitAsync(Timeout);
        var invalidation = Invalidate(kind, services, cache, request);

        try
        {
            Assert.False(invalidation.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, invalidation).WaitAsync(Timeout);
        }

        Assert.Equal("old", await first);
        Assert.False(cache.TryGetValue(request.GetCacheEntryKey(), out _));
        Assert.Equal(kind == InvalidationKind.Entry, cache.TryGetValue(other.GetCacheEntryKey(), out _));
        Assert.Equal("new", await behavior.HandleAsync(request,
            new Next<Query>((_, _) => ValueTask.FromResult("new")), default));
        Assert.Equal("new", cache.Get<string>(request.GetCacheEntryKey()));
    }

    [Fact]
    public async Task TypeInvalidation_CanceledAfterPartialAcquisition_ShouldReleaseOnlyAcquiredGates()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache, gatesCount: 3);
        var locks = services.Locks<Query>();
        var field = typeof(CacheRequestTypeLocksStriping<Query>)
            .GetField("_gates", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var gates = Assert.IsType<SemaphoreSlim[]>(field.GetValue(locks));
        await gates[2].WaitAsync();
        using var source = new CancellationTokenSource();
        var invalidation = services.Invalidation.InvalidateAsync(typeof(Query), source.Token).AsTask();

        try
        {
            Assert.All(gates, gate => Assert.Equal(0, gate.CurrentCount));
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invalidation.WaitAsync(Timeout));
            Assert.Equal(1, gates[0].CurrentCount);
            Assert.Equal(1, gates[1].CurrentCount);
            Assert.Equal(0, gates[2].CurrentCount);
        }
        finally
        {
            source.Cancel();
            gates[2].Release();
        }

        await services.Invalidation.InvalidateAsync(typeof(Query), default).AsTask().WaitAsync(Timeout);
        Assert.All(gates, gate => Assert.Equal(1, gate.CurrentCount));
    }

    [Fact]
    public async Task EntryInvalidation_CanceledWaiter_ShouldNotReleaseWriterGate()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var request = new Query();
        var key = request.GetCacheEntryKey();
        var gate = services.Locks<Query>().ForRequest(key);
        await gate.WaitAsync();
        using var source = new CancellationTokenSource();
        var invalidation = services.Invalidation.InvalidateAsync(typeof(Query), source.Token, key).AsTask();

        try
        {
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invalidation.WaitAsync(Timeout));
            Assert.Equal(0, gate.CurrentCount);
        }
        finally
        {
            gate.Release();
        }
    }

    [Fact]
    public async Task ConcurrentTypeInvalidations_ShouldCompleteWithoutDeadlock()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        cache.Set(new Query().GetCacheEntryKey(), "old");

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(async () =>
            await services.Invalidation.InvalidateAsync(typeof(Query), default)))).WaitAsync(Timeout);

        Assert.False(cache.TryGetValue(new Query().GetCacheEntryKey(), out _));
        Assert.Equal(1, services.Locks<Query>().ForRequest(new Query().GetCacheEntryKey()).CurrentCount);
    }

    [Fact]
    public async Task RegisteredPipeline_ShouldShareLocksAcrossScopesAndWaitForWrites()
    {
        var services = new ServiceCollection();
        services.AddMitMediator(typeof(IMediator).Assembly);
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(StripedInvalidationTests).Assembly]);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var value = "old";
        var calls = 0;
        services.AddSingleton<IRequestHandler<Query, string>>(new Handler<Query>(async (_, _) =>
        {
            var snapshot = value;
            if (++calls == 1)
            {
                started.SetResult();
                await release.Task.WaitAsync(Timeout);
            }
            return snapshot;
        }));
        services.AddSingleton<IRequestHandler<Update, string>>(new Handler<Update>((_, _) =>
        {
            value = "new";
            return ValueTask.FromResult("updated");
        }));
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstMediator = firstScope.ServiceProvider.GetRequiredService<IMediator>();
        var secondMediator = secondScope.ServiceProvider.GetRequiredService<IMediator>();
        var request = new Query();
        var first = firstMediator.SendAsync<Query, string>(request, default).AsTask();
        await started.Task.WaitAsync(Timeout);
        var update = secondMediator.SendAsync<Update, string>(new Update(), default).AsTask();

        try
        {
            Assert.False(update.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, update).WaitAsync(Timeout);
        }

        Assert.Equal("old", await first);
        Assert.Equal("new", await secondMediator.SendAsync<Query, string>(request, default));
        Assert.Equal("new", await firstMediator.SendAsync<Query, string>(request, default));
        Assert.Equal(2, calls);
    }

    private static Task Invalidate(
        InvalidationKind kind, TestCacheServices services, MemoryCache cache, Query request)
    {
        var matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(Update)] = [typeof(Query)],
            [typeof(CachedUpdate)] = [typeof(Query)]
        });
        switch (kind)
        {
            case InvalidationKind.Trigger:
                return new InMemoryClearCacheByMatrixBehavior<Update, string>(services.Invalidation, matrix)
                    .HandleAsync(new Update(), new Next<Update>((_, _) => ValueTask.FromResult("updated")), default)
                    .AsTask();
            case InvalidationKind.CachedTrigger:
                var trigger = new CachedUpdate();
                cache.Set(trigger.GetCacheEntryKey(), "cached update");
                var cacheBehavior = new InMemoryCacheBehavior<CachedUpdate, string>(
                    cache, services.Locks<CachedUpdate>(), services.Invalidation);
                return new InMemoryClearCacheByMatrixBehavior<CachedUpdate, string>(services.Invalidation, matrix)
                    .HandleAsync(trigger, new Next<CachedUpdate>((actual, token) =>
                        cacheBehavior.HandleAsync(actual,
                            new Next<CachedUpdate>((_, _) => throw new InvalidOperationException("Expected cache hit")),
                            token)), default).AsTask();
            case InvalidationKind.Type:
                return new ClearAllResponsesCacheNotificationHandler(services.Invalidation)
                    .HandleAsync(new ClearAllResponsesCacheNotification(typeof(Query)), default).AsTask();
            case InvalidationKind.Entry:
                return new ClearResponseCacheForRequestHandler(services.Invalidation)
                    .HandleAsync(new ClearResponseCacheForRequestNotification(request), default).AsTask();
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public enum InvalidationKind { Trigger, CachedTrigger, Type, Entry }

    [CacheResponseInMemory]
    private class BaseQuery : IRequest<string>;

    private sealed class UnannotatedDerivedQuery : BaseQuery;

    [CacheResponseInMemory]
    private sealed class DerivedQuery : BaseQuery;

    [CacheResponseInMemory(typeof(Update), typeof(CachedUpdate))]
    private sealed class Query : IRequest<string>
    {
        public int Id { get; init; }
    }

    private sealed class Update : IRequest<string>;

    [CacheResponseInMemory]
    private sealed class CachedUpdate : IRequest<string>;

    private sealed class Next<TRequest>(Func<TRequest, CancellationToken, ValueTask<string>> invoke)
        : IRequestHandlerNext<TRequest, string> where TRequest : IRequest<string>
    {
        public ValueTask<string> InvokeAsync(TRequest request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }

    private sealed class Handler<TRequest>(Func<TRequest, CancellationToken, ValueTask<string>> invoke)
        : IRequestHandler<TRequest, string> where TRequest : IRequest<string>
    {
        public ValueTask<string> HandleAsync(TRequest request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }
}
