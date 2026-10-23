using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache.Tests;

public class InMemoryCacheBehaviorConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task HandleAsync_DifferentKeysOnSameStripe_ShouldSerializeAndKeepSeparateEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache, gatesCount: 1);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        var firstRequest = new Query { Id = 1 };
        var secondRequest = new Query { Id = 2 };
        var first = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation)
            .HandleAsync(firstRequest, new Next(async (_, _) =>
            {
                started.SetResult();
                await release.Task.WaitAsync(Timeout);
                return "first";
            }), default).AsTask();
        await started.Task.WaitAsync(Timeout);
        var second = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation)
            .HandleAsync(secondRequest, new Next((_, _) =>
            {
                secondStarted = true;
                return ValueTask.FromResult("second");
            }), default).AsTask();

        try
        {
            Assert.False(secondStarted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(Timeout);
        }

        Assert.Equal("first", cache.Get<string>(firstRequest.GetCacheEntryKey()));
        Assert.Equal("second", cache.Get<string>(secondRequest.GetCacheEntryKey()));
    }

    [Fact]
    public async Task HandleAsync_ConcurrentMisses_ShouldExecuteHandlerOnceAcrossBehaviorInstances()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var next = new Next(async (_, _) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(Timeout);
            return "response";
        });
        var request = new Query();
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation)
                .HandleAsync(request, next, CancellationToken.None).AsTask())
            .ToArray();

        release.SetResult();
        var responses = await Task.WhenAll(tasks).WaitAsync(Timeout);

        Assert.All(responses, response => Assert.Equal("response", response));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task HandleAsync_DifferentKeys_ShouldNotBlockEachOther()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = new Query { Id = 1 };
        var first = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation).HandleAsync(
            firstRequest, new Next(async (_, _) =>
            {
                await release.Task.WaitAsync(Timeout);
                return "first";
            }), CancellationToken.None).AsTask();

        try
        {
            var request = Enumerable.Range(2, 1000).Select(id => new Query { Id = id })
                .First(candidate => services.Locks<Query>().ForRequest(candidate.GetCacheEntryKey()) !=
                    services.Locks<Query>().ForRequest(firstRequest.GetCacheEntryKey()));
            var second = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation).HandleAsync(
                request, new Next((_, _) => ValueTask.FromResult("second")), CancellationToken.None);

            Assert.Equal("second", await second.AsTask().WaitAsync(Timeout));
        }
        finally
        {
            release.TrySetResult();
            await first.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task HandleAsync_NullResult_ShouldKeepSameLockWhileWaitersRemain()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var next = new Next(async (_, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                await releaseFirst.Task.WaitAsync(Timeout);
                return null!;
            }

            if (call == 2)
            {
                secondStarted.SetResult();
                await releaseSecond.Task.WaitAsync(Timeout);
            }

            return "response";
        });
        var behavior = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation);
        var request = new Query();
        var first = behavior.HandleAsync(request, next, CancellationToken.None).AsTask();
        var second = behavior.HandleAsync(request, next, CancellationToken.None).AsTask();
        Task<string>? third = null;

        try
        {
            releaseFirst.SetResult();
            Assert.Null(await first.WaitAsync(Timeout));
            await secondStarted.Task.WaitAsync(Timeout);

            third = behavior.HandleAsync(request, next, CancellationToken.None).AsTask();
            Assert.Equal(2, Volatile.Read(ref calls));

            releaseSecond.SetResult();
            Assert.Equal("response", await second.WaitAsync(Timeout));
            Assert.Equal("response", await third.WaitAsync(Timeout));
            Assert.Equal(2, calls);
        }
        finally
        {
            releaseFirst.TrySetResult();
            releaseSecond.TrySetResult();
            await Task.WhenAll(first, second, third ?? Task.FromResult("")).WaitAsync(Timeout);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_CanceledWaiter_ShouldNotExecuteOrReleaseActiveLock(bool alreadyCanceled)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(cache);
        using var source = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var next = new Next(async (_, _) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(Timeout);
            return "response";
        });
        var behavior = new InMemoryCacheBehavior<Query, string>(cache, services.Locks<Query>(), services.Invalidation);
        var request = new Query();
        var first = behavior.HandleAsync(request, next, CancellationToken.None).AsTask();
        Task<string>? third = null;

        try
        {
            if (alreadyCanceled)
            {
                source.Cancel();
            }

            var canceled = behavior.HandleAsync(request, next, source.Token).AsTask();
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(Timeout));

            third = behavior.HandleAsync(request, next, CancellationToken.None).AsTask();
            Assert.Equal(1, Volatile.Read(ref calls));

            release.SetResult();
            Assert.Equal("response", await first.WaitAsync(Timeout));
            Assert.Equal("response", await third.WaitAsync(Timeout));
            Assert.Equal(1, calls);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first, third ?? Task.FromResult("")).WaitAsync(Timeout);
        }
    }

    [CacheResponseInMemory]
    private sealed class Query : IRequest<string>
    {
        public int Id { get; init; }
    }

    private sealed class Next(Func<Query, CancellationToken, ValueTask<string>> invoke)
        : IRequestHandlerNext<Query, string>
    {
        public ValueTask<string> InvokeAsync(Query request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }
}
