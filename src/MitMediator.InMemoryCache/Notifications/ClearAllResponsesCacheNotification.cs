namespace MitMediator.InMemoryCache.Notifications;

internal class ClearAllResponsesCacheNotification(Type requestType) : INotification
{
    public Type RequestType { get; private set; } = requestType;
}

internal class ClearAllResponsesCacheNotificationHandler(CacheInvalidationService invalidationService)
    : INotificationHandler<ClearAllResponsesCacheNotification>
{
    public ValueTask HandleAsync(ClearAllResponsesCacheNotification cacheNotification, CancellationToken cancellationToken)
    {
        return invalidationService.InvalidateAsync(cacheNotification.RequestType, cancellationToken);
    }
}