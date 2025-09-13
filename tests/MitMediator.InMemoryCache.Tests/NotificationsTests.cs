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
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set("String_key1", "value1");
        memoryCache.Set("String_key2", "value2");
        memoryCache.Set("Int32_key3", "value3");
        var handler = new ClearAllResponsesCacheNotificationHandler(memoryCache);
        var notification = new ClearAllResponsesCacheNotification(typeof(string));
        await handler.HandleAsync(notification, CancellationToken.None);
        Assert.False(memoryCache.TryGetValue("String_key1", out _));
        Assert.False(memoryCache.TryGetValue("String_key2", out _));
        Assert.True(memoryCache.TryGetValue("Int32_key3", out _));
    }

    [Fact]
    public void ClearResponseCacheForRequestNotification_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ClearResponseCacheForRequestNotification(null));
    }

    [Fact]
    public async Task ClearResponseCacheForRequestHandler_RemovesKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var request = "test-request";
        var key = request.GetCacheEntryKey();
        memoryCache.Set(key, "value");
        var handler = new ClearResponseCacheForRequestHandler(memoryCache);
        var notification = new ClearResponseCacheForRequestNotification(request);
        await handler.HandleAsync(notification, CancellationToken.None);
        Assert.False(memoryCache.TryGetValue(key, out _));
    }
}
