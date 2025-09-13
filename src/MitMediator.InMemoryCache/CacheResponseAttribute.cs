namespace MitMediator.InMemoryCache;

/// <summary>
/// Specifies caching behavior for request handlers, allowing responses to be cached 
/// indefinitely, for a fixed duration, or until triggered by other request types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public class CacheResponseAttribute : Attribute
{
    public int? AbsoluteExpirationRelativeToNowSeconds { get; private set; }

    public int EntrySize { get; private set; } = 1;

    public Type[]? RequestsToClearCache { get; private set; }

    /// <summary>
    /// Caches the response until one of the specified request types is received.
    /// </summary>
    /// <param name="requestsToClearCache">Types of requests that will trigger cache invalidation.</param>
    public CacheResponseAttribute(params Type[]? requestsToClearCache)
    {
        if (requestsToClearCache is not null && requestsToClearCache.Length > 0)
        {
            RequestsToClearCache = requestsToClearCache;
        }
    }

    /// <summary>
    /// Caches the response for a specified duration or indefinitely.
    /// </summary>
    /// <param name="expirationSeconds">Absolute expiration time in seconds, relative to the current moment. Set null for indefinitely</param>
    /// <param name="entrySize">The size of the cache entry item. For collections, size is calculated per element.</param>
    /// <param name="requestsToClearCache">Types of requests that will trigger cache invalidation</param>
    public CacheResponseAttribute(
        int expirationSeconds = -1, 
        int entrySize = 1,
        params Type[] requestsToClearCache)
    {
        EntrySize = entrySize;
        AbsoluteExpirationRelativeToNowSeconds = expirationSeconds == -1
            ? null
            : expirationSeconds;
        RequestsToClearCache = requestsToClearCache;
    }
}