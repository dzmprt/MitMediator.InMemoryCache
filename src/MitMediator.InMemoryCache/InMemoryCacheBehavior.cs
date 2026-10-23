using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace MitMediator.InMemoryCache;

internal class InMemoryCacheBehavior<TRequest, TResponse>(
    MemoryCache memoryCache,
    CacheRequestTypeLocksStriping<TRequest> cacheRequestTypeLocksStriping,
    CacheInvalidationService invalidationService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        IRequestHandlerNext<TRequest, TResponse> nextPipe,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);

        var cacheAttribute = requestType.GetCustomAttribute<CacheResponseInMemoryAttribute>();
        if (cacheAttribute is null)
        {
            return await nextPipe.InvokeAsync(request, cancellationToken);
        }

        var runtimeRequestType = request.GetType();
        invalidationService.RegisterRequestType(runtimeRequestType);
        var key = request.GetCacheEntryKey();
        if (memoryCache.TryGetValue(key, out var responseFromCache))
        {
            return (TResponse)responseFromCache!;
        }

        var gate = runtimeRequestType == requestType
            ? cacheRequestTypeLocksStriping.ForRequest(key)
            : invalidationService.GetRequestTypeLocks(runtimeRequestType).ForRequest(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (memoryCache.TryGetValue(key, out responseFromCache))
            {
                return (TResponse)responseFromCache!;
            }

            var result = await nextPipe.InvokeAsync(request, cancellationToken);

            if (result == null)
            {
                return result;
            }

            SetMemoryCacheValue(result, key, cacheAttribute);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private void SetMemoryCacheValue(
        TResponse response,
        string key,
        CacheResponseInMemoryAttribute
        cacheResponseInMemoryAttribute)
    {
        long entrySize = cacheResponseInMemoryAttribute.EntrySize;
        if (response is ICollection collection)
        {
            entrySize = entrySize * collection.Count;
        }

        var memoryCacheEntryOptions = new MemoryCacheEntryOptions
        {
            Size = entrySize
        };

        if (cacheResponseInMemoryAttribute.AbsoluteExpirationRelativeToNowSeconds.HasValue)
        {
            memoryCacheEntryOptions.AbsoluteExpirationRelativeToNow =
                new TimeSpan(0, 0, cacheResponseInMemoryAttribute.AbsoluteExpirationRelativeToNowSeconds.Value);
        }

        memoryCache.Set(key, response, memoryCacheEntryOptions);
    }
}