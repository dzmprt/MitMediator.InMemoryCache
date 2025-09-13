using System.Text.Json;

namespace MitMediator.InMemoryCache;

internal static class ObjectGetCacheEntryKeyExtensions
{
    public static string GetCacheEntryKey(this object obj)
    {
        // DON'T USE GetHashCode()
        return $"{obj.GetType().Name}_{JsonSerializer.Serialize(obj)}";
    }
}