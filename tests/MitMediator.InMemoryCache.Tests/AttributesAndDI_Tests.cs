using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MitMediator.InMemoryCache.Tests;

public class AttributeTests
{
    [Fact]
    public void CacheForeverAttribute_ShouldImplementICacheAttribute()
    {
        var attr = new CacheForeverAttribute();
        Assert.IsAssignableFrom<ICacheAttribute>(attr);
    }

    [Fact]
    public void CacheForSecondsAttribute_ShouldSetCacheTime()
    {
        var attr = new CacheForSecondsAttribute(42);
        Assert.Equal(TimeSpan.FromSeconds(42), attr.CacheTime);
    }

    [Fact]
    public void CacheUntilSentAttribute_ShouldSetTriggers()
    {
        var types = new[] { typeof(string), typeof(int) };
        var attr = new CacheUntilSentAttribute(types);
        Assert.Equal(types, attr.TriggersToClearRequests);
    }
}