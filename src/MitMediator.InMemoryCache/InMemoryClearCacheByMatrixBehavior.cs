namespace MitMediator.InMemoryCache;

internal class InMemoryClearCacheByMatrixBehavior<TRequest, TResponse>(
    CacheInvalidationService invalidationService,
    ClearCacheMatrix clearCacheMatrix)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, IRequestHandlerNext<TRequest, TResponse> nextPipe,
        CancellationToken cancellationToken)
    {
        var result = await nextPipe.InvokeAsync(request, cancellationToken);
        var requestType = typeof(TRequest);

        if (clearCacheMatrix.ClearCacheMatrixDictionary.TryGetValue(requestType, out var typesToClearCache))
        {
            foreach (var typeToClearCache in typesToClearCache)
            {
                await invalidationService.InvalidateAsync(typeToClearCache, CancellationToken.None);
            }
        }
        return result;
    }
}