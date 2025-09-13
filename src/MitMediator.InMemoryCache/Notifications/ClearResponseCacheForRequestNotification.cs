using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache.Notifications;

internal class ClearResponseCacheForRequestNotification : INotification
{
    public object Request { get; private set; }
    
    // public Type RequestType { get; private set; } = requestType;

    public ClearResponseCacheForRequestNotification(object request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        Request = request;
    }
}

internal class ClearResponseCacheForRequestHandler(MemoryCache memoryCache) : INotificationHandler<ClearResponseCacheForRequestNotification>
{
    public ValueTask HandleAsync(ClearResponseCacheForRequestNotification notification, CancellationToken cancellationToken)
    {
        var key = notification.Request!.GetCacheEntryKey();
        memoryCache.Remove(key);
        return ValueTask.CompletedTask;
    }
}