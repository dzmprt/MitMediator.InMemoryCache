using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using MitMediator;
using MitMediator.InMemoryCache;
using Xunit;

namespace MitMediator.InMemoryCache.Tests;

public class InMemoryCacheBehaviorSizeTests
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions { SizeLimit = 1000, TrackStatistics = true});

    [Fact]
    public async Task CacheEntry_Size_Is_One_For_Single_Response()
    {
        var behavior = new InMemoryCacheBehavior<SingleRequest, string>(_memoryCache);
        var request = new SingleRequest();
        var next = new SingleHandlerNext();
        await behavior.HandleAsync(request, next, CancellationToken.None);
        
        Assert.Equal(1, _memoryCache.GetCurrentStatistics()!.CurrentEstimatedSize);
    }

    [Fact]
    public async Task CacheEntry_Size_Equals_Collection_Count()
    {
        var behavior = new InMemoryCacheBehavior<CollectionRequest, List<string>>(_memoryCache);
        var request = new CollectionRequest();
        var next = new CollectionHandlerNext();
         
        await behavior.HandleAsync(request, next, CancellationToken.None);
        
        Assert.Equal(3, _memoryCache.GetCurrentStatistics()!.CurrentEstimatedSize); 
    }
    

    [CacheForever]
    private class SingleRequest : IRequest<string> { }
    
    [CacheForever]
    private class CollectionRequest : IRequest<List<string>> { }
    private class SingleHandlerNext : IRequestHandlerNext<SingleRequest, string>
    {
        public ValueTask<string> InvokeAsync(SingleRequest request, CancellationToken cancellationToken) => new("single");
    }
    private class CollectionHandlerNext : IRequestHandlerNext<CollectionRequest, List<string>>
    {
        public ValueTask<List<string>> InvokeAsync(CollectionRequest request, CancellationToken cancellationToken) => new(new List<string> { "a", "b", "c" });
    }
}
