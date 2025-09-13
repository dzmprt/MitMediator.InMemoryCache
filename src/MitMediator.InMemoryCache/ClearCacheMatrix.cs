using System.Collections.Concurrent;

namespace MitMediator.InMemoryCache;

internal static class ClearCacheMatrix
{
    public static IReadOnlyDictionary<Type, Type[]> ClearCacheMatrixDictionary { get; set; } =
        new Dictionary<Type, Type[]>();
}