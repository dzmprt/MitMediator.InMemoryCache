using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache.Tests;

public class InMemoryCacheBehaviorTests
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    [Fact]
    public async Task HandleAsync_ShouldCacheResult_WhenCacheForeverAttributeIsPresent()
    {
        var behavior = new InMemoryCacheBehavior<TestRequest, string>(_memoryCache);
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
        var behavior = new InMemoryCacheBehavior<NoCacheRequest, string>(_memoryCache);
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
        var behavior = new InMemoryCacheBehavior<CacheForSecondsRequest, string>(_memoryCache);
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
        ClearCacheMatrix.ClearCacheMatrixDictionary = new Dictionary<Type, Type[]>
        {
            { typeof(ClearCacheRequest), [type] }
        };
        var behavior = new InMemoryCacheBehavior<CachedRequest, string>(_memoryCache);
        var request = new CachedRequest
        {
            TestData = "test"
        };
        var next = new TestRequestHandlerNext<string>(() => Task.FromResult("matrix"));

        await behavior.HandleAsync(request, next, CancellationToken.None);

        var key = $"{type.Name}_" + System.Text.Json.JsonSerializer.Serialize(request);
        Assert.True(_memoryCache.TryGetValue(key, out _));

        var clearCacheRequest = new ClearCacheRequest();
        var behaviorForClear = new InMemoryCacheBehavior<ClearCacheRequest, string>(_memoryCache);
        await behaviorForClear.HandleAsync(clearCacheRequest, next, CancellationToken.None);

        Assert.False(_memoryCache.TryGetValue(key, out _));
    }

    [CacheUntilSent(typeof(ClearCacheRequest))]
    internal class CachedRequest : IRequest<string>
    {
        public string TestData { get; set; }
    }

    internal class ClearCacheRequest : IRequest<string>;

    [CacheForever]
    internal class TestRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    internal class NoCacheRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    [CacheForSeconds(10)]
    internal class CacheForSecondsRequest : IRequest<string>
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public async Task HandleAsync_ShouldNotCacheNullResult()
    {
        var behavior = new InMemoryCacheBehavior<TestRequest, string?>(_memoryCache);
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