using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MitMediator.InMemoryCache;

internal interface ICacheRequestTypeLocksStriping
{
    SemaphoreSlim ForRequest(string key);

    ValueTask InvalidateAsync(MemoryCache cache, string? key, CancellationToken cancellationToken);
}

public class CacheRequestTypeLocksStriping<RequestType> : ICacheRequestTypeLocksStriping, IDisposable
{
    private readonly SemaphoreSlim[] _gates;

    public CacheRequestTypeLocksStriping(IOptions<CacheRequestTypeLocksStripingOptions> options)
    {
        _gates = Enumerable.Range(0, options.Value.LockStripingGatesCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    }

    public SemaphoreSlim ForRequest(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var hash = unchecked((uint)StringComparer.Ordinal.GetHashCode(key));
        var index = (int)(hash % (uint)_gates.Length);

        return _gates[index];
    }

    async ValueTask ICacheRequestTypeLocksStriping.InvalidateAsync(
        MemoryCache cache, string? key, CancellationToken cancellationToken)
    {
        var gates = key is null ? _gates : [ForRequest(key)];
        var acquired = 0;
        try
        {
            // fixed deadlocking
            for (; acquired < gates.Length; acquired++)
            {
                await gates[acquired].WaitAsync(cancellationToken);
            }

            if (key is not null)
            {
                cache.Remove(key);
                return;
            }

            var prefix = typeof(RequestType).GetCacheEntryKeyPrefix();
            foreach (var cacheKey in cache.Keys.OfType<string>())
            {
                if (cacheKey.StartsWith(prefix, StringComparison.Ordinal))
                {
                    cache.Remove(cacheKey);
                }
            }
        }
        finally
        {
            for (var i = acquired - 1; i >= 0; i--)
            {
                gates[i].Release();
            }
        }
    }

    public void Dispose()
    {
        foreach (var gate in _gates)
        {
            gate.Dispose();
        }
    }
}