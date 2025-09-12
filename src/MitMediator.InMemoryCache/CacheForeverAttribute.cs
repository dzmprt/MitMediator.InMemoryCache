namespace MitMediator.InMemoryCache;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CacheForeverAttribute : Attribute, ICacheAttribute;