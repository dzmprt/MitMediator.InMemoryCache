using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache.Tests;

public class InMemoryCacheBehaviorEdgeCaseTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly TestCacheServices _services;
    private ClearCacheMatrix _matrix = new(new Dictionary<Type, Type[]>());

    public InMemoryCacheBehaviorEdgeCaseTests() => _services = new TestCacheServices(_cache);

    public void Dispose()
    {
        _services.Dispose();
        _cache.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_Trigger_ShouldInvalidateOnCacheMissAndHit(bool cacheHit)
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedTrigger)] = [typeof(CachedQuery), typeof(ExpiringQuery)]
        });
        var first = new CachedQuery { Id = 1 };
        var second = new CachedQuery { Id = 2 };
        var expiring = new ExpiringQuery();
        var trigger = new CachedTrigger();
        _cache.Set(first.GetCacheEntryKey(), "first");
        _cache.Set(second.GetCacheEntryKey(), "second");
        _cache.Set(expiring.GetCacheEntryKey(), "expiring");
        _cache.Set("unrelated", "keep");
        _cache.Set(42, "non-string key");
        if (cacheHit)
        {
            _cache.Set(trigger.GetCacheEntryKey(), "cached trigger");
        }
        var calls = 0;
        var next = new Next<CachedTrigger, string>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult("handler result");
        });

        var result = await HandleWithInvalidation(trigger, next, CancellationToken.None);

        Assert.Equal(cacheHit ? "cached trigger" : "handler result", result);
        Assert.Equal(cacheHit ? 0 : 1, calls);
        Assert.False(_cache.TryGetValue(first.GetCacheEntryKey(), out _));
        Assert.False(_cache.TryGetValue(second.GetCacheEntryKey(), out _));
        Assert.False(_cache.TryGetValue(expiring.GetCacheEntryKey(), out _));
        Assert.Equal("keep", _cache.Get<string>("unrelated"));
        Assert.Equal("non-string key", _cache.Get<string>(42));
        Assert.True(_cache.TryGetValue(trigger.GetCacheEntryKey(), out _));
    }

    [Fact]
    public async Task HandleAsync_DistinctRequestValues_ShouldHaveIndependentEntries()
    {
        var behavior = new InMemoryCacheBehavior<CachedQuery, string>(_cache, _services.Locks<CachedQuery>(), _services.Invalidation);
        var calls = 0;
        var next = new Next<CachedQuery, string>((request, _) =>
        {
            calls++;
            return ValueTask.FromResult($"response {request.Id}");
        });

        Assert.Equal("response 1", await behavior.HandleAsync(new CachedQuery { Id = 1 }, next, CancellationToken.None));
        Assert.Equal("response 2", await behavior.HandleAsync(new CachedQuery { Id = 2 }, next, CancellationToken.None));
        Assert.Equal("response 1", await behavior.HandleAsync(new CachedQuery { Id = 1 }, next, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task HandleAsync_AbsoluteExpiration_ShouldNotBeExtendedByCacheHits()
    {
        var clock = new TestClock();
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var services = new TestCacheServices(cache);
        var behavior = new InMemoryCacheBehavior<ExpiringQuery, string>(cache, services.Locks<ExpiringQuery>(), services.Invalidation);
        var request = new ExpiringQuery();
        var calls = 0;
        var next = new Next<ExpiringQuery, string>((_, _) => ValueTask.FromResult($"response {++calls}"));

        Assert.Equal("response 1", await behavior.HandleAsync(request, next, CancellationToken.None));
        clock.UtcNow += TimeSpan.FromSeconds(9);
        Assert.Equal("response 1", await behavior.HandleAsync(request, next, CancellationToken.None));
        clock.UtcNow += TimeSpan.FromSeconds(1);
        Assert.Equal("response 2", await behavior.HandleAsync(request, next, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task HandleAsync_NoExpiration_ShouldRetainEntryAfterTimeAdvances()
    {
        var clock = new TestClock();
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
        using var services = new TestCacheServices(cache);
        var behavior = new InMemoryCacheBehavior<CachedQuery, string>(cache, services.Locks<CachedQuery>(), services.Invalidation);
        var request = new CachedQuery();
        var calls = 0;
        var next = new Next<CachedQuery, string>((_, _) => ValueTask.FromResult($"response {++calls}"));

        Assert.Equal("response 1", await behavior.HandleAsync(request, next, CancellationToken.None));
        clock.UtcNow += TimeSpan.FromDays(365);
        Assert.Equal("response 1", await behavior.HandleAsync(request, next, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_DownstreamException_ShouldNotCacheOrInvalidate(bool cancellation)
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedTrigger)] = [typeof(CachedQuery)]
        });
        var query = new CachedQuery();
        _cache.Set(query.GetCacheEntryKey(), "keep");
        var request = new CachedTrigger();
        using var source = new CancellationTokenSource();
        Exception expected = cancellation
            ? new OperationCanceledException(source.Token)
            : new InvalidOperationException("handler failed");
        var calls = 0;
        var next = new Next<CachedTrigger, string>((actualRequest, token) =>
        {
            Assert.Same(request, actualRequest);
            Assert.Equal(source.Token, token);
            calls++;
            return ValueTask.FromException<string>(expected);
        });
        for (var i = 0; i < 2; i++)
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => HandleWithInvalidation(request, next, source.Token).AsTask());
            Assert.Same(expected, exception);
        }

        Assert.Equal(2, calls);
        Assert.False(_cache.TryGetValue(request.GetCacheEntryKey(), out _));
        Assert.Equal("keep", _cache.Get<string>(query.GetCacheEntryKey()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_UncachedTriggerException_ShouldNotInvalidate(bool cancellation)
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(UncachedTrigger)] = [typeof(CachedQuery)]
        });
        var key = new CachedQuery().GetCacheEntryKey();
        _cache.Set(key, "keep");
        Exception expected = cancellation
            ? new OperationCanceledException()
            : new InvalidOperationException("handler failed");
        var next = new Next<UncachedTrigger, string>((_, _) => ValueTask.FromException<string>(expected));

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            HandleWithInvalidation(new UncachedTrigger(), next, CancellationToken.None).AsTask());

        Assert.Same(expected, exception);
        Assert.Equal("keep", _cache.Get<string>(key));
    }

    [Fact]
    public async Task HandleAsync_CanceledCacheMiss_ShouldNotExecuteOrInvalidateAndAllowRetry()
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedTrigger)] = [typeof(CachedQuery)]
        });
        var key = new CachedQuery().GetCacheEntryKey();
        _cache.Set(key, "keep");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var calls = 0;
        var next = new Next<CachedTrigger, string>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult("response");
        });
        var request = new CachedTrigger();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HandleWithInvalidation(request, next, source.Token).AsTask());

        Assert.Equal(0, calls);
        Assert.Equal("keep", _cache.Get<string>(key));
        Assert.Equal("response", await HandleWithInvalidation(request, next, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.False(_cache.TryGetValue(key, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_Trigger_ShouldInvalidateOnlyAfterSuccessfulHandler(bool cachedTrigger)
    {
        var triggerType = cachedTrigger ? typeof(CachedTrigger) : typeof(UncachedTrigger);
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [triggerType] = [typeof(CachedQuery)]
        });
        var query = new CachedQuery();
        var key = query.GetCacheEntryKey();
        _cache.Set(key, "before");

        ValueTask<string> Handle()
        {
            Assert.Equal("before", _cache.Get<string>(key));
            _cache.Set(key, "read during update");
            return ValueTask.FromResult("updated");
        }

        if (cachedTrigger)
        {
            await HandleWithInvalidation(
                new CachedTrigger(), new Next<CachedTrigger, string>((_, _) => Handle()), CancellationToken.None);
        }
        else
        {
            await HandleWithInvalidation(
                new UncachedTrigger(), new Next<UncachedTrigger, string>((_, _) => Handle()), CancellationToken.None);
        }

        Assert.False(_cache.TryGetValue(key, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_CacheBehaviorAlone_ShouldNotInvalidate(bool cacheHit)
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedTrigger)] = [typeof(CachedQuery)]
        });
        var key = new CachedQuery().GetCacheEntryKey();
        _cache.Set(key, "keep");
        var trigger = new CachedTrigger();
        if (cacheHit)
        {
            _cache.Set(trigger.GetCacheEntryKey(), "cached");
        }

        var result = await new InMemoryCacheBehavior<CachedTrigger, string>(_cache, _services.Locks<CachedTrigger>(), _services.Invalidation).HandleAsync(
            trigger, new Next<CachedTrigger, string>((_, _) => ValueTask.FromResult("handled")),
            CancellationToken.None);

        Assert.Equal(cacheHit ? "cached" : "handled", result);
        Assert.Equal("keep", _cache.Get<string>(key));
    }

    [Fact]
    public async Task HandleAsync_NullTriggerResult_ShouldInvalidateWithoutCachingNull()
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(CachedTrigger)] = [typeof(CachedQuery)]
        });
        var key = new CachedQuery().GetCacheEntryKey();
        _cache.Set(key, "stale");
        var trigger = new CachedTrigger();
        var calls = 0;
        var next = new Next<CachedTrigger, string>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult<string>(null!);
        });

        Assert.Null(await HandleWithInvalidation(trigger, next, CancellationToken.None));
        Assert.False(_cache.TryGetValue(key, out _));
        Assert.False(_cache.TryGetValue(trigger.GetCacheEntryKey(), out _));
        Assert.Null(await HandleWithInvalidation(trigger, next, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task HandleAsync_Collection_ShouldMultiplyCustomEntrySize(int count)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100, TrackStatistics = true });
        using var services = new TestCacheServices(cache);
        var response = Enumerable.Range(0, count).ToArray();
        var request = new CollectionQuery();
        var next = new Next<CollectionQuery, int[]>((_, _) => ValueTask.FromResult(response));

        var result = await new InMemoryCacheBehavior<CollectionQuery, int[]>(cache, services.Locks<CollectionQuery>(), services.Invalidation)
            .HandleAsync(request, next, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Same(response, cache.Get<int[]>(request.GetCacheEntryKey()));
        Assert.Equal(count * 4, cache.GetCurrentStatistics()!.CurrentEstimatedSize);
    }

    [Fact]
    public async Task HandleAsync_EntryExceedingSizeLimit_ShouldNotBeCached()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 3 });
        using var services = new TestCacheServices(cache);
        var behavior = new InMemoryCacheBehavior<CollectionQuery, int[]>(cache, services.Locks<CollectionQuery>(), services.Invalidation);
        var request = new CollectionQuery();
        var response = new[] { 1 };
        var calls = 0;
        var next = new Next<CollectionQuery, int[]>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult(response);
        });

        Assert.Same(response, await behavior.HandleAsync(request, next, CancellationToken.None));
        Assert.Same(response, await behavior.HandleAsync(request, next, CancellationToken.None));
        Assert.False(cache.TryGetValue(request.GetCacheEntryKey(), out _));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypeInvalidation_ShouldPreserveOtherTypesWithSimilarNames(bool explicitInvalidation)
    {
        _matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            [typeof(UncachedTrigger)] = [typeof(FirstGroup.Query)]
        });
        var key = new FirstGroup.Query().GetCacheEntryKey();
        var similarKey = new FirstGroup.QueryDetails().GetCacheEntryKey();
        var otherKey = new SecondGroup.Query().GetCacheEntryKey();
        _cache.Set(key, "remove");
        _cache.Set(similarKey, "similar name");
        _cache.Set(otherKey, "other type");

        if (explicitInvalidation)
        {
            await new ClearAllResponsesCacheNotificationHandler(_services.Invalidation).HandleAsync(
                new ClearAllResponsesCacheNotification(typeof(FirstGroup.Query)), CancellationToken.None);
        }
        else
        {
            await HandleWithInvalidation(new UncachedTrigger(),
                new Next<UncachedTrigger, string>((_, _) => ValueTask.FromResult("updated")), CancellationToken.None);
        }

        Assert.False(_cache.TryGetValue(key, out _));
        Assert.Equal("similar name", _cache.Get<string>(similarKey));
        Assert.Equal("other type", _cache.Get<string>(otherKey));
    }

    private static class FirstGroup
    {
        public sealed class Query : IRequest<string>;
        public sealed class QueryDetails : IRequest<string>;
    }

    private static class SecondGroup
    {
        public sealed class Query : IRequest<string>;
    }

    private ValueTask<TResponse> HandleWithInvalidation<TRequest, TResponse>(
        TRequest request, IRequestHandlerNext<TRequest, TResponse> next, CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var cacheBehavior = new InMemoryCacheBehavior<TRequest, TResponse>(_cache, _services.Locks<TRequest>(), _services.Invalidation);
        var cacheNext = new Next<TRequest, TResponse>((actualRequest, token) =>
            cacheBehavior.HandleAsync(actualRequest, next, token));

        return new InMemoryClearCacheByMatrixBehavior<TRequest, TResponse>(_services.Invalidation, _matrix)
            .HandleAsync(request, cacheNext, cancellationToken);
    }

    [CacheResponseInMemory]
    private class CachedQuery : IRequest<string>
    {
        public int Id { get; init; }
    }

    [CacheResponseInMemory]
    private class CachedTrigger : IRequest<string>;

    private class UncachedTrigger : IRequest<string>;

    [CacheResponseInMemory(10)]
    private class ExpiringQuery : IRequest<string>;

    [CacheResponseInMemory(30, 4)]
    private class CollectionQuery : IRequest<int[]>;

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }

    private sealed class Next<TRequest, TResponse>(
        Func<TRequest, CancellationToken, ValueTask<TResponse>> invoke) : IRequestHandlerNext<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public ValueTask<TResponse> InvokeAsync(TRequest request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }
}
