using System.Diagnostics.CodeAnalysis;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache.Tests;

public class MediatorExtensionsTests
{
    [ExcludeFromCodeCoverage]
    private class DummyMediator : IMediator
    {
        public object? PublishedNotification { get; private set; }
        public CancellationToken? PublishedToken { get; private set; }
        
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : IRequest<TResponse>
        {
            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken) where TRequest : IRequest
        {
            throw new NotImplementedException();
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : IRequest<TResponse>
        {
            throw new NotImplementedException();
        }

        public ValueTask<Unit> SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken) where TRequest : IRequest
        {
            throw new NotImplementedException();
        }

        public ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken) where TNotification : INotification
        {
            PublishedNotification = notification;
            PublishedToken = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public Task PublishParallelAsync<TNotification>(TNotification notification,
            CancellationToken cancellationToken = new CancellationToken()) where TNotification : INotification
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : IStreamRequest<TResponse>
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task ClearResponseCacheAsync_PublishesNotification()
    {
        var mediator = new DummyMediator();
        var request = "test-request";
        var token = new CancellationTokenSource().Token;
        await mediator.ClearResponseCacheAsync(request, token);
        Assert.NotNull(mediator.PublishedNotification);
        Assert.IsType<ClearResponseCacheForRequestNotification>(mediator.PublishedNotification);
        Assert.Equal(token, mediator.PublishedToken);
    }

    [Fact]
    public async Task ClearAllResponseCacheAsync_PublishesNotification()
    {
        var mediator = new DummyMediator();
        var token = new CancellationTokenSource().Token;
        await mediator.ClearAllResponseCacheAsync<string>(token);
        Assert.NotNull(mediator.PublishedNotification);
        Assert.IsType<ClearAllResponsesCacheNotification>(mediator.PublishedNotification);
        Assert.Equal(token, mediator.PublishedToken);
    }
}
