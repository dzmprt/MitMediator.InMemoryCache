using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator;

var services = new ServiceCollection();
services
    .AddMitMediator()
    .AddMitMediatorInMemoryCache(new MemoryCacheOptions {SizeLimit = 100});

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

// Get a result from handler.
string result = await mediator.SendAsync<PingRequest, string>(new PingRequest(), CancellationToken.None);
Console.WriteLine(result); //Pong result

// Get a result from cache.
result = await mediator.SendAsync<PingRequest, string>(new PingRequest(), CancellationToken.None);
Console.WriteLine(result); //Pong result

[CacheResponseInMemory]
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