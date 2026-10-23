namespace MitMediator.InMemoryCache.Tests;

public class AttributeTests
{
    [Fact]
    public void CacheResponseAttribute_CacheForever_ShouldHaveEntrySize1ByDefaultAndOtherDataNull()
    {
        var attr = new CacheResponseInMemoryAttribute();
        Assert.Equal(1, attr.EntrySize);
        Assert.Null(attr.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Null(attr.RequestsToClearCache);

    }

    [Fact]
    public void CacheResponseAttribute_CacheForTime_ShouldSetCacheTimeAndSize()
    {
        var attr = new CacheResponseInMemoryAttribute(42, 2);
        Assert.Equal(42, attr.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(2, attr.EntrySize);

    }

    [Fact]
    public void CacheResponseAttribute_CacheUntilTrigger_ShouldSetTriggers()
    {
        var types = new[] { typeof(string), typeof(int) };
        var attr = new CacheResponseInMemoryAttribute(types);
        Assert.Equal(types, attr.RequestsToClearCache);
    }

    [Fact]
    public void CacheResponseInMemoryAttribute_NullTriggers_ShouldKeepDefaults()
    {
        var attribute = new CacheResponseInMemoryAttribute((Type[]?)null);

        Assert.Null(attribute.RequestsToClearCache);
        Assert.Null(attribute.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(1, attribute.EntrySize);
    }

    [Fact]
    public void CacheResponseInMemoryAttribute_EmptyTriggers_ShouldKeepDefaults()
    {
        var attribute = new CacheResponseInMemoryAttribute(Array.Empty<Type>());

        Assert.Null(attribute.RequestsToClearCache);
        Assert.Null(attribute.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(1, attribute.EntrySize);
    }

    [Fact]
    public void CacheResponseInMemoryAttribute_IndefiniteLifetime_ShouldPreserveSizeAndTriggers()
    {
        var triggers = new[] { typeof(string), typeof(int) };
        var attribute = new CacheResponseInMemoryAttribute(-1, 4, triggers);

        Assert.Null(attribute.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(4, attribute.EntrySize);
        Assert.Same(triggers, attribute.RequestsToClearCache);
    }

    [Fact]
    public void CacheResponseInMemoryAttribute_FixedLifetime_ShouldPreserveTriggers()
    {
        var triggers = new[] { typeof(string) };
        var attribute = new CacheResponseInMemoryAttribute(30, 2, triggers);

        Assert.Equal(30, attribute.AbsoluteExpirationRelativeToNowSeconds);
        Assert.Equal(2, attribute.EntrySize);
        Assert.Same(triggers, attribute.RequestsToClearCache);
    }

    [Fact]
    public void CacheResponseInMemoryAttribute_ShouldNotBeInherited()
    {
        var usage = Assert.Single(typeof(CacheResponseInMemoryAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false));
        var attributeUsage = Assert.IsType<AttributeUsageAttribute>(usage);

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, attributeUsage.ValidOn);
        Assert.False(attributeUsage.AllowMultiple);
        Assert.False(attributeUsage.Inherited);
        Assert.Empty(typeof(DerivedRequest).GetCustomAttributes(typeof(CacheResponseInMemoryAttribute), true));
    }

    [CacheResponseInMemory]
    private class BaseRequest;

    private class DerivedRequest : BaseRequest;
}