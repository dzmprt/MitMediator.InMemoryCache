using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MitMediator.InMemoryCache;
using Xunit;

namespace MitMediator.InMemoryCache.Tests;

public class ClearCacheMatrixTests
{
    [Fact]
    public void ClearCacheMatrixDictionary_SetAndGet_WorksCorrectly()
    {
        var dict = new Dictionary<Type, Type[]> { { typeof(string), new[] { typeof(int), typeof(double) } } };
        ClearCacheMatrix.ClearCacheMatrixDictionary = dict;
        Assert.NotNull(ClearCacheMatrix.ClearCacheMatrixDictionary);
        Assert.True(ClearCacheMatrix.ClearCacheMatrixDictionary.ContainsKey(typeof(string)));
        Assert.Equal(new[] { typeof(int), typeof(double) }, ClearCacheMatrix.ClearCacheMatrixDictionary[typeof(string)]);
    }
}
