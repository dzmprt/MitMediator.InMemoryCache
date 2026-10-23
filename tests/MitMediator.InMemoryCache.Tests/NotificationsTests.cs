using Microsoft.Extensions.Caching.Memory;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache.Tests;

public class NotificationsTests
{
    [Fact]
    public void ClearAllResponsesCacheNotification_SetsRequestType()
    {
        var type = typeof(string);
        var notification = new ClearAllResponsesCacheNotification(type);
        Assert.Equal(type, notification.RequestType);
    }

    [Fact]
    public async Task ClearAllResponsesCacheNotificationHandler_RemovesAllMatchingKeys()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(memoryCache);
        memoryCache.Set("Type:System.String_SerializedRequest:key1", "value1");
        memoryCache.Set("Type:System.String_SerializedRequest:key2", "value2");
        memoryCache.Set("Type:System.Int32_SerializedRequest:key3", "value3");
        memoryCache.Set("Type:System.StringBuilder_SerializedRequest:key4", "prefix match");
        memoryCache.Set("Type:Other.String_SerializedRequest:key5", "other namespace");
        memoryCache.Set("Type:system.string_SerializedRequest:key6", "different case");
        memoryCache.Set(42, "non-string key");
        var handler = new ClearAllResponsesCacheNotificationHandler(services.Invalidation);
        var notification = new ClearAllResponsesCacheNotification(typeof(string));
        await handler.HandleAsync(notification, CancellationToken.None);
        Assert.False(memoryCache.TryGetValue("Type:System.String_SerializedRequest:key1", out _));
        Assert.False(memoryCache.TryGetValue("Type:System.String_SerializedRequest:key2", out _));
        Assert.True(memoryCache.TryGetValue("Type:System.Int32_SerializedRequest:key3", out _));
        Assert.Equal("prefix match", memoryCache.Get<string>("Type:System.StringBuilder_SerializedRequest:key4"));
        Assert.Equal("other namespace", memoryCache.Get<string>("Type:Other.String_SerializedRequest:key5"));
        Assert.Equal("different case", memoryCache.Get<string>("Type:system.string_SerializedRequest:key6"));
        Assert.Equal("non-string key", memoryCache.Get<string>(42));
    }

    [Fact]
    public void ClearResponseCacheForRequestNotification_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ClearResponseCacheForRequestNotification(null));
    }

    [Fact]
    public async Task ClearResponseCacheForRequestHandler_RemovesKey()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var services = new TestCacheServices(memoryCache);
        var request = "test-request";
        var key = request.GetCacheEntryKey();
        memoryCache.Set(key, "value");
        var handler = new ClearResponseCacheForRequestHandler(services.Invalidation);
        var notification = new ClearResponseCacheForRequestNotification(request);
        await handler.HandleAsync(notification, CancellationToken.None);
        Assert.False(memoryCache.TryGetValue(key, out _));
    }
}
