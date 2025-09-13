using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache;

public class InMemoryCacheBehavior<TRequest, TResponse>(MemoryCache memoryCache)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, IRequestHandlerNext<TRequest, TResponse> nextPipe,
        CancellationToken cancellationToken)
    {
        TResponse result;
        var requestType = typeof(TRequest);

        var cacheAttribute = requestType.GetCustomAttribute<CacheResponseAttribute>();

        if (cacheAttribute is null)
        {
            result = await nextPipe.InvokeAsync(request, cancellationToken);
            ClearCacheByMatrix(requestType);
            return result;
        }
        
        var key = request.GetCacheEntryKey();

        if (memoryCache.TryGetValue(key, out var value))
        {
            ClearCacheByMatrix(requestType);
            return (TResponse)value;
        }

        result = await nextPipe.InvokeAsync(request, cancellationToken);
        ClearCacheByMatrix(requestType);

        if (result is null)
        {
            return result;
        }

        var entrySize = cacheAttribute.EntrySize;
        if (result is ICollection collection)
        {
            entrySize = entrySize * collection.Count;
        }

        var memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            Size = entrySize
        };
        
        // TODO: check by benchmarks
        // if (!cacheAttribute.AbsoluteExpirationRelativeToNowSeconds.HasValue &&
        //     cacheAttribute.RequestsToClearCache is null)
        // {
        //     memoryCacheEntryOptions.Priority = CacheItemPriority.NeverRemove;
        // }

        if (cacheAttribute.AbsoluteExpirationRelativeToNowSeconds.HasValue)
        {
            memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow =
                new TimeSpan(0, 0, cacheAttribute.AbsoluteExpirationRelativeToNowSeconds.Value);
        }

        memoryCache.Set(key, result, memoryCacheEntryOptions);

        return result;
    }

    // TODO: background task?
    private void ClearCacheByMatrix(Type request)
    {
        if (ClearCacheMatrix.ClearCacheMatrixDictionary.TryGetValue(request, out var typesToClearCache))
        {
            var requestsTypeName = typesToClearCache.Select(c => c.Name).ToArray();
            foreach (var requestTypeName in requestsTypeName)
            {
                var keys = memoryCache
                    .Keys
                    .Where(k => k is string && k.ToString()?.StartsWith(requestTypeName) == true)
                    .Select(k => k.ToString()).ToArray();

                foreach (var key in keys)
                {
                    memoryCache.Remove(key!);
                }
            }
        }
    }
}