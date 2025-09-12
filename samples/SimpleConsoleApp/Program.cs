using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator;
using MitMediator.InMemoryCache;

var services = new ServiceCollection();
services
    .AddMitMediator(typeof(PingRequestHandler).Assembly)
    .AddRequestsInMemoryCache(new MemoryCacheOptions {SizeLimit = 100}, typeof(PingRequestHandler).Assembly);

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

// Get a result from handler.
string result = await mediator.SendAsync<PingRequest, string>(new PingRequest(), CancellationToken.None);
Console.WriteLine(result); //Pong result

// Get a result from cache.
result = await mediator.SendAsync<PingRequest, string>(new PingRequest(), CancellationToken.None);
Console.WriteLine(result); //Pong result

[CacheForever]
public class PingRequest : IRequest<string>;

public class PingRequestHandler : IRequestHandler<PingRequest, string>
{
    private static bool _isReturned;

    public ValueTask<string> HandleAsync(PingRequest request, CancellationToken cancellationToken)
    {
        if (_isReturned)
        {
            throw new Exception();
        }
        _isReturned = true;
        return ValueTask.FromResult("Pong result");
    }
}