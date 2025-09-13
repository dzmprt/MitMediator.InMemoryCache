using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache.Notifications;

internal class ClearAllResponsesCacheNotification(Type requestType) : INotification
{
    public Type RequestType { get; private set; } = requestType;
}

internal class ClearAllResponsesCacheNotificationHandler(MemoryCache memoryCache)
    : INotificationHandler<ClearAllResponsesCacheNotification>
{
    public ValueTask HandleAsync(ClearAllResponsesCacheNotification cacheNotification, CancellationToken cancellationToken)
    {
        var keyStart = cacheNotification.RequestType.Name;
        var entriesKeys = memoryCache.Keys
            .Where(k => k is string && k.ToString()?.StartsWith(keyStart) == true);
        foreach (var entriesKey in entriesKeys)
        {
            memoryCache.Remove(entriesKey);
        }
        return ValueTask.CompletedTask;
    }
}