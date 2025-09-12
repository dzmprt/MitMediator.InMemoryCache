namespace MitMediator.InMemoryCache;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CacheForSecondsAttribute(int seconds) : Attribute, ICacheAttribute
{
    public TimeSpan CacheTime { get; } = new(0,0, seconds);
}