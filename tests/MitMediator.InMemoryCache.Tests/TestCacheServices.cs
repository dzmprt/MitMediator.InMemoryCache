using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MitMediator.InMemoryCache.Tests;

internal sealed class TestCacheServices : IDisposable
{
    private readonly ServiceProvider _provider;

    public TestCacheServices(MemoryCache cache, int gatesCount = 64)
    {
        var services = new ServiceCollection();
        services.AddSingleton(cache);
        services.AddOptions<CacheRequestTypeLocksStripingOptions>()
            .Configure(options => options.LockStripingGatesCount = gatesCount);
        services.AddSingleton(typeof(CacheRequestTypeLocksStriping<>), typeof(CacheRequestTypeLocksStriping<>));
        services.AddSingleton<CacheInvalidationService>();
        _provider = services.BuildServiceProvider();
    }

    public CacheRequestTypeLocksStriping<TRequest> Locks<TRequest>() =>
        _provider.GetRequiredService<CacheRequestTypeLocksStriping<TRequest>>();

    public CacheInvalidationService Invalidation => _provider.GetRequiredService<CacheInvalidationService>();

    public void Dispose() => _provider.Dispose();
}
