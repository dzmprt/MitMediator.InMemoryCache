using System.Text.Json;

namespace MitMediator.InMemoryCache;

internal static class ObjectGetCacheEntryKeyExtensions
{
    internal const string TypePrefix = "Type:";
    internal const string SerializedRequestSeparator = "_SerializedRequest:";

    public static string GetCacheEntryKey(this object obj)
    {
        return $"{GetCacheEntryKeyPrefix(obj.GetType())}{JsonSerializer.Serialize(obj)}";
    }

    public static string GetCacheEntryKeyPrefix(this Type requestType) =>
        $"{TypePrefix}{requestType.FullName}{SerializedRequestSeparator}";
}