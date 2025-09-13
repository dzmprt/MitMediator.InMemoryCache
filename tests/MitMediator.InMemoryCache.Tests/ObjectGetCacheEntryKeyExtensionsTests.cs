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
        Assert.Equal("TestObj_{\"Id\":1,\"Name\":\"Test\",\"InnerObject\":{\"BoolValue\":true}}", key);
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