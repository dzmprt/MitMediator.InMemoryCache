using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache;

public static class MediatorExtensions
{
    /// <summary>
    /// Clear response cache for request data.
    /// </summary>
    /// <param name="mediator"><see cref="IMediator"/>.</param>
    /// <param name="request">Request.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
    /// <typeparam name="TRequest">Type of request.</typeparam>
    /// <returns></returns>
    public static ValueTask ClearResponseCacheAsync<TRequest>(this IMediator mediator, TRequest request, CancellationToken cancellationToken)
    {
        var notification = new ClearResponseCacheForRequestNotification(request);
        return mediator.PublishAsync(notification, cancellationToken);
    }
    
    /// <summary>
    /// Clear all response cache for request.
    /// </summary>
    /// <param name="mediator"><see cref="IMediator"/>.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
    /// <typeparam name="TRequest">Type of request.</typeparam>
    /// <returns></returns>
    public static ValueTask ClearAllResponseCacheAsync<TRequest>(this IMediator mediator, CancellationToken cancellationToken)
    {
        var notification = new ClearAllResponsesCacheNotification(typeof(TRequest));
        return mediator.PublishAsync(notification, cancellationToken);
    }
}