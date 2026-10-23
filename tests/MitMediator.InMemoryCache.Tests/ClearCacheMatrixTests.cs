namespace MitMediator.InMemoryCache.Tests;

public class ClearCacheMatrixTests
{
    [Fact]
    public void Constructor_ShouldStoreRules()
    {
        var dict = new Dictionary<Type, Type[]> { { typeof(string), new[] { typeof(int), typeof(double) } } };
        var matrix = new ClearCacheMatrix(dict);

        Assert.Equal(new[] { typeof(int), typeof(double) }, matrix.ClearCacheMatrixDictionary[typeof(string)]);
    }

    [Fact]
    public void Instances_ShouldHaveIndependentRules()
    {
        var first = new ClearCacheMatrix(new Dictionary<Type, Type[]> { [typeof(string)] = [typeof(int)] });
        var second = new ClearCacheMatrix(new Dictionary<Type, Type[]>());

        Assert.Equal(new[] { typeof(int) }, first.ClearCacheMatrixDictionary[typeof(string)]);
        Assert.Empty(second.ClearCacheMatrixDictionary);
    }
}
