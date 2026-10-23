namespace MitMediator.InMemoryCache;

public sealed class CacheRequestTypeLocksStripingOptions
{
    public int LockStripingGatesCount { get; set; } = 64;
}
