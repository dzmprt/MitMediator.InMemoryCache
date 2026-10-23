namespace MitMediator.InMemoryCache;

internal sealed class ClearCacheMatrix(IReadOnlyDictionary<Type, Type[]> clearCacheMatrixDictionary)
{
    public IReadOnlyDictionary<Type, Type[]> ClearCacheMatrixDictionary { get; } = clearCacheMatrixDictionary;
}