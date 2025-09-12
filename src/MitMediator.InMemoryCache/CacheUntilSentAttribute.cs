namespace MitMediator.InMemoryCache;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class CacheUntilSentAttribute(params Type[] triggersToClearRequests) : Attribute, ICacheAttribute
{
    public Type[] TriggersToClearRequests { get; } = triggersToClearRequests;
}