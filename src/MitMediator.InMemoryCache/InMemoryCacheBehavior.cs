using System.Collections;
using System.Text.Json;
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

        var cacheAttribute = requestType
            .GetCustomAttributes(inherit: true)
            .OfType<ICacheAttribute>()
            .SingleOrDefault();

        if (cacheAttribute is null)
        {
            result = await nextPipe.InvokeAsync(request, cancellationToken);
            ClearCacheByMatrix(requestType);
            return result;
        }

        // DON'T USE GetHashCode()
        var key = $"{requestType.Name}_{JsonSerializer.Serialize(request)}";
        
        if (memoryCache.TryGetValue(key, out var value))
        {
            ClearCacheByMatrix(requestType);
            return (TResponse)value;
        }

        result = await nextPipe.InvokeAsync(request, cancellationToken);
        ClearCacheByMatrix(requestType);

        if(result is null)
        {
            return result;
        }

        var entrySize = 1;
        if (result is ICollection collection)
        {
            entrySize = collection.Count;
        }
        switch (cacheAttribute)
        {
            case CacheForeverAttribute:
                memoryCache.Set(key, result,  new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove, Size = entrySize });
                break;
            case CacheUntilSentAttribute:
                memoryCache.Set(key, result, new MemoryCacheEntryOptions { Size = entrySize });
                break;
            case CacheForSecondsAttribute cacheForSecondsAttribute:
                memoryCache.Set(key, result, new MemoryCacheEntryOptions { Size = entrySize, AbsoluteExpirationRelativeToNow = cacheForSecondsAttribute.CacheTime});
                break;
        }

        return result;
    }

    // TODO: background task?
    private void ClearCacheByMatrix(Type request)
    {
        if (ClearCacheMatrix.ClearCacheMatrixDictionary is null)
        {
            return;
        }

        if (ClearCacheMatrix.ClearCacheMatrixDictionary.TryGetValue(request, out var typesToClearCache))
        {
            var requestsToClearCacheNames = typesToClearCache.Select(c => c.Name).ToArray();
            foreach (var typesToClearCacheName in requestsToClearCacheNames)
            {
                var keys = memoryCache
                    .Keys
                    .Where(k => k.ToString()!.StartsWith(typesToClearCacheName))
                    .Select(k => k.ToString()).ToArray();
                        
                foreach (var key in keys)
                {
                    memoryCache.Remove(key!);
                }
            }
        }
    }
}