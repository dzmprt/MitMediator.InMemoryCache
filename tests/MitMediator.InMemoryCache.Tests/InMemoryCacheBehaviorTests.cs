using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache.Tests;

public class InMemoryCacheBehaviorTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly TestCacheServices _services;

    public InMemoryCacheBehaviorTests() => _services = new TestCacheServices(_memoryCache);

    public void Dispose()
    {
        _services.Dispose();
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task HandleAsync_ShouldCacheResult_WhenCacheForeverAttributeIsPresent()
    {
        var behavior = new InMemoryCacheBehavior<TestRequest, string>(_memoryCache, _services.Locks<TestRequest>(), _services.Invalidation);
        var request = new TestRequest { Value = "test" };
        var nextCalled = 0;
        var next = new TestRequestHandlerNext<string>(() =>
        {
            nextCalled++;
            return Task.FromResult("response");
        });

        var response1 = await behavior.HandleAsync(request, next, CancellationToken.None);
        var response2 = await behavior.HandleAsync(request, next, CancellationToken.None);

        Assert.Equal("response", response1);
        Assert.Equal("response", response2);
        Assert.Equal(1, nextCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotCache_WhenNoCacheAttribute()
    {
        var behavior = new InMemoryCacheBehavior<NoCacheRequest, string>(_memoryCache, _services.Locks<NoCacheRequest>(), _services.Invalidation);
        var request = new NoCacheRequest { Value = "test" };
        var nextCalled = 0;
        var next = new TestRequestHandlerNext<string>(() =>
        {
            nextCalled++;
            return Task.FromResult("response");
        });

        var response1 = await behavior.HandleAsync(request, next, CancellationToken.None);
        var response2 = await behavior.HandleAsync(request, next, CancellationToken.None);

        Assert.Equal("response", response1);
        Assert.Equal("response", response2);
        Assert.Equal(2, nextCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldCacheForSeconds()
    {
        var behavior = new InMemoryCacheBehavior<CacheForSecondsRequest, string>(_memoryCache, _services.Locks<CacheForSecondsRequest>(), _services.Invalidation);
        var request = new CacheForSecondsRequest { Value = "test" };
        var nextCalled = 0;
        var next = new TestRequestHandlerNext<string>(() =>
        {
            nextCalled++;
            return Task.FromResult("response");
        });

        var response1 = await behavior.HandleAsync(request, next, CancellationToken.None);
        var response2 = await behavior.HandleAsync(request, next, CancellationToken.None);

        Assert.Equal("response", response1);
        Assert.Equal("response", response2);
        Assert.Equal(1, nextCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldCacheAndClearCacheByMatrix()
    {
        var type = typeof(CachedRequest);
        var matrix = new ClearCacheMatrix(new Dictionary<Type, Type[]>
        {
            { typeof(ClearCacheRequest), [type] }
        });
        var behavior = new InMemoryCacheBehavior<CachedRequest, string>(_memoryCache, _services.Locks<CachedRequest>(), _services.Invalidation);
        var request = new CachedRequest
        {
            TestData = "test"
        };
        var next = new TestRequestHandlerNext<string>(() => Task.FromResult("matrix"));

        await behavior.HandleAsync(request, next, CancellationToken.None);

        var key = request.GetCacheEntryKey();
        Assert.True(_memoryCache.TryGetValue(key, out _));

        var clearCacheRequest = new ClearCacheRequest();
        var behaviorForClear = new InMemoryClearCacheByMatrixBehavior<ClearCacheRequest, string>(_services.Invalidation, matrix);
        await behaviorForClear.HandleAsync(clearCacheRequest, next, CancellationToken.None);

        Assert.False(_memoryCache.TryGetValue(key, out _));
    }

    [CacheResponseInMemory(typeof(ClearCacheRequest))]
    internal class CachedRequest : IRequest<string>
    {
        public string TestData { get; set; }
    }

    internal class ClearCacheRequest : IRequest<string>;

    [CacheResponseInMemory]
    internal class TestRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    internal class NoCacheRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    [CacheResponseInMemory(10)]
    internal class CacheForSecondsRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public async Task HandleAsync_ShouldNotCacheNullResult()
    {
        var behavior = new InMemoryCacheBehavior<TestRequest, string?>(_memoryCache, _services.Locks<TestRequest>(), _services.Invalidation);
        var request = new TestRequest { Value = "test-null" };
        int nextCalled = 0;
        var next = new TestRequestHandlerNext<string?>(() => { nextCalled++; return Task.FromResult<string?>(null); });

        var response1 = await behavior.HandleAsync(request, next, CancellationToken.None);
        var response2 = await behavior.HandleAsync(request, next, CancellationToken.None);

        Assert.Null(response1);
        Assert.Null(response2);
        Assert.Equal(2, nextCalled);
    }
    
    internal class TestRequestHandlerNext<T>(Func<Task<T>> func) :
        IRequestHandlerNext<TestRequest, T>,
        IRequestHandlerNext<NoCacheRequest, T>,
        IRequestHandlerNext<CacheForSecondsRequest, T>,
        IRequestHandlerNext<CachedRequest, T>,
        IRequestHandlerNext<ClearCacheRequest, T>
    {
        public ValueTask<T> InvokeAsync(TestRequest request, CancellationToken cancellationToken) => new(func());
        public ValueTask<T> InvokeAsync(NoCacheRequest request, CancellationToken cancellationToken) => new(func());

        public ValueTask<T> InvokeAsync(CacheForSecondsRequest request, CancellationToken cancellationToken) =>
            new(func());

        public ValueTask<T> InvokeAsync(CachedRequest request, CancellationToken cancellationToken) => new(func());

        public ValueTask<T> InvokeAsync(ClearCacheRequest newRequest, CancellationToken cancellationToken) =>
            new(func());
    }
}