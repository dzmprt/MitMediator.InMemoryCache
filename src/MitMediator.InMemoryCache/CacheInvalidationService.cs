using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using static MitMediator.InMemoryCache.ObjectGetCacheEntryKeyExtensions;

namespace MitMediator.InMemoryCache;

internal sealed class CacheInvalidationService(MemoryCache cache, IServiceProvider serviceProvider)
{
    private readonly ConcurrentDictionary<Type, byte> _closedGenericRequestTypes = new();

    public void RegisterRequestType(Type requestType)
    {
        if (requestType.IsConstructedGenericType)
        {
            _closedGenericRequestTypes.TryAdd(requestType, 0);
        }
    }

    public async ValueTask InvalidateAsync(Type requestType, CancellationToken cancellationToken, string? key = null)
    {
        if (requestType.IsGenericTypeDefinition)
        {
            var closedTypes = _closedGenericRequestTypes.Keys
                .Where(type => type.GetGenericTypeDefinition() == requestType)
                .ToHashSet();
            var prefix = $"{TypePrefix}{requestType.FullName}[";
            foreach (var cacheKey in cache.Keys.OfType<string>())
            {
                if (!cacheKey.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = cacheKey.IndexOf(SerializedRequestSeparator, StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                var typeName = cacheKey[TypePrefix.Length..separatorIndex];
                closedTypes.Add(requestType.Assembly.GetType(typeName, throwOnError: true)!);
            }
            foreach (var closedType in closedTypes)
            {
                await InvalidateAsync(closedType, cancellationToken);
            }
            return;
        }

        await GetRequestTypeLocks(requestType).InvalidateAsync(cache, key, cancellationToken);
    }

    public ICacheRequestTypeLocksStriping GetRequestTypeLocks(Type requestType)
    {
        var locksType = typeof(CacheRequestTypeLocksStriping<>).MakeGenericType(requestType);
        return (ICacheRequestTypeLocksStriping)serviceProvider.GetRequiredService(locksType);
    }
}
