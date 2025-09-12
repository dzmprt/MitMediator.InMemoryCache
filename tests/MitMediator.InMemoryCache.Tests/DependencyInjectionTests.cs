using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MitMediator.InMemoryCache.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddRequestsInMemoryCache_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddRequestsInMemoryCache();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<MemoryCache>());
    }

    [Fact]
    public void AddRequestsInMemoryCache_WithOptions_ShouldRegisterWithOptions()
    {
        var services = new ServiceCollection();
        var options = new MemoryCacheOptions { SizeLimit = 123 };
        services.AddRequestsInMemoryCache(options);
        var provider = services.BuildServiceProvider();
        var cache = provider.GetService<MemoryCache>()!;
        Assert.NotNull(cache);
    }
}