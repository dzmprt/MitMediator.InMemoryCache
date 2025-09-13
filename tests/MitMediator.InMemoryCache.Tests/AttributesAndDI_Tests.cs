using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MitMediator.InMemoryCache.Tests;

public class AttributeTests
{
    [Fact]
    public void CacheResponseAttribute_CacheForever_ShouldHaveEntrySize1ByDefaultAndOtherDataNull()
    {
        var attr = new CacheResponseAttribute();
        Assert.Equal(1, attr.EntrySize);
        Assert.Null(attr.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Null(attr.RequestsToClearCache);

    }

    [Fact]
    public void CacheResponseAttribute_CacheForTime_ShouldSetCacheTimeAndSize()
    {
        var attr = new CacheResponseAttribute(42, 2);
        Assert.Equal(42, attr.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(2, attr.EntrySize);

    }

    [Fact]
    public void CacheResponseAttribute_CacheUntilTrigger_ShouldSetTriggers()
    {
        var types = new[] { typeof(string), typeof(int) };
        var attr = new CacheResponseAttribute(types);
        Assert.Equal(types, attr.RequestsToClearCache);
    }
}