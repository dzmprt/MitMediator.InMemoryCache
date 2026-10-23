using System;
using Xunit;
using MitMediator.InMemoryCache;

namespace MitMediator.InMemoryCache.Tests;

public class CacheEntryKeyUtilTests
{
    [Fact]
    public void GetCacheEntryKey_ReturnsExpectedFormat()
    {
        var obj = new TestObj { Id = 1, Name = "Test", InnerObject = new InnerObject() { BoolValue = true } };
        var key = obj.GetCacheEntryKey();
        Assert.Equal($"Type:{typeof(TestObj).FullName}_SerializedRequest:{{\"Id\":1,\"Name\":\"Test\",\"InnerObject\":{{\"BoolValue\":true}}}}", key);
    }

    [Fact]
    public void GetCacheEntryKey_SameSimpleName_ShouldIncludeDeclaringType()
    {
        var first = new FirstGroup.Query().GetCacheEntryKey();
        var second = new SecondGroup.Query().GetCacheEntryKey();

        Assert.Equal(typeof(FirstGroup.Query).Name, typeof(SecondGroup.Query).Name);
        Assert.Equal($"Type:{typeof(FirstGroup.Query).FullName}_SerializedRequest:{{}}", first);
        Assert.Equal($"Type:{typeof(SecondGroup.Query).FullName}_SerializedRequest:{{}}", second);
        Assert.NotEqual(first, second);
    }

    private static class FirstGroup
    {
        public sealed class Query;
    }

    private static class SecondGroup
    {
        public sealed class Query;
    }

    private class TestObj
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public InnerObject InnerObject { get; set; }
    }

    private class InnerObject
    {
        public bool BoolValue { get; set; }
    }
}