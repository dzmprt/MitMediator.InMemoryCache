namespace MitMediator.InMemoryCache.Notifications;

internal class ClearResponseCacheForRequestNotification : INotification
{
    public object Request { get; private set; }

    public ClearResponseCacheForRequestNotification(object request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        Request = request;
    }
}

internal class ClearResponseCacheForRequestHandler(CacheInvalidationService invalidationService) : INotificationHandler<ClearResponseCacheForRequestNotification>
{
    public ValueTask HandleAsync(ClearResponseCacheForRequestNotification notification, CancellationToken cancellationToken)
    {
        var key = notification.Request!.GetCacheEntryKey();
        return invalidationService.InvalidateAsync(notification.Request.GetType(), cancellationToken, key);
    }
}