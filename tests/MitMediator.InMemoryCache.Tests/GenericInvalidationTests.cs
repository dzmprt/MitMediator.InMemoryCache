using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MitMediator.InMemoryCache.Tests;

public class GenericInvalidationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trigger_ShouldInvalidateDirectEntriesOfUnusedClosedVariants(bool usedVariant)
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var first = new Query<int> { Id = 1 };
        var second = new Query<int> { Id = 2 };
        var text = new Query<string>();
        var nested = new Query<Dictionary<string, int>>();
        var unrelated = new OtherQuery<int>();
        if (usedVariant)
        {
            await mediator.SendAsync<Query<int>, string>(first, default);
        }
        cache.Set(first.GetCacheEntryKey(), "first");
        cache.Set(second.GetCacheEntryKey(), "second");
        cache.Set(text.GetCacheEntryKey(), "text");
        cache.Set(nested.GetCacheEntryKey(), "nested");
        cache.Set(unrelated.GetCacheEntryKey(), "keep");
        cache.Set(42, "non-string key");

        await mediator.SendAsync<Update, string>(new Update(), default);

        Assert.False(cache.TryGetValue(first.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(second.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(text.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(nested.GetCacheEntryKey(), out _));
        Assert.Equal("keep", cache.Get<string>(unrelated.GetCacheEntryKey()));
        Assert.Equal("non-string key", cache.Get<string>(42));
    }

    [Fact]
    public async Task Trigger_WithDirectGenericEntry_ShouldWaitForItsWriteStripe()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var request = new Query<int>();
        var key = request.GetCacheEntryKey();
        cache.Set(key, "old");
        var gate = provider.GetRequiredService<CacheRequestTypeLocksStriping<Query<int>>>().ForRequest(key);
        await gate.WaitAsync();
        var invalidation = mediator.SendAsync<Update, string>(new Update(), default).AsTask();

        try
        {
            Assert.False(invalidation.IsCompleted);
        }
        finally
        {
            gate.Release();
            await invalidation.WaitAsync(Timeout);
        }

        Assert.False(cache.TryGetValue(key, out _));
    }

    [Fact]
    public async Task Trigger_BeforeAnyClosedVariantIsUsed_ShouldSucceed()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Equal("updated", await mediator.SendAsync<Update, string>(new Update(), default));
        Assert.Equal("int", await mediator.SendAsync<Query<int>, string>(new Query<int>(), default));
    }

    [Fact]
    public async Task Trigger_ShouldInvalidateAllClosedVariantsAndParameterValuesAcrossScopes()
    {
        var services = CreateServices();
        var calls = 0;
        services.AddSingleton<IRequestHandler<Query<int>, string>>(
            new Handler<Query<int>>((_, _) => ValueTask.FromResult($"int {++calls}")));
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstMediator = firstScope.ServiceProvider.GetRequiredService<IMediator>();
        var secondMediator = secondScope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var first = new Query<int> { Id = 1 };
        var second = new Query<int> { Id = 2 };
        var text = new Query<string>();
        var unrelated = new OtherQuery<int>();

        Assert.Equal("int 1", await firstMediator.SendAsync<Query<int>, string>(first, default));
        Assert.Equal("int 1", await secondMediator.SendAsync<Query<int>, string>(first, default));
        Assert.Equal("int 2", await secondMediator.SendAsync<Query<int>, string>(second, default));
        Assert.Equal("string", await firstMediator.SendAsync<Query<string>, string>(text, default));
        Assert.Equal("other", await firstMediator.SendAsync<OtherQuery<int>, string>(unrelated, default));
        Assert.True(cache.TryGetValue(text.GetCacheEntryKey(), out _));

        Assert.Equal("updated", await secondMediator.SendAsync<Update, string>(new Update(), default));

        Assert.False(cache.TryGetValue(first.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(second.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(text.GetCacheEntryKey(), out _));
        Assert.Equal("other", cache.Get<string>(unrelated.GetCacheEntryKey()));
        Assert.Equal("int 3", await firstMediator.SendAsync<Query<int>, string>(first, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitInvalidation_ShouldPreserveOtherClosedVariants(bool singleEntry)
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var cache = provider.GetRequiredService<MemoryCache>();
        var first = new Query<int> { Id = 1 };
        var second = new Query<int> { Id = 2 };
        var text = new Query<string>();
        await mediator.SendAsync<Query<int>, string>(first, default);
        await mediator.SendAsync<Query<int>, string>(second, default);
        await mediator.SendAsync<Query<string>, string>(text, default);

        if (singleEntry)
        {
            await mediator.ClearResponseCacheAsync(first, default);
        }
        else
        {
            await mediator.ClearAllResponseCacheAsync<Query<int>>(default);
        }

        Assert.False(cache.TryGetValue(first.GetCacheEntryKey(), out _));
        Assert.Equal(singleEntry, cache.TryGetValue(second.GetCacheEntryKey(), out _));
        Assert.Equal("string", cache.Get<string>(text.GetCacheEntryKey()));
    }

    [Fact]
    public async Task Trigger_ShouldWaitForInFlightWritesOfAllClosedVariants()
    {
        var services = CreateServices();
        var intStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stringStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.AddSingleton<IRequestHandler<Query<int>, string>>(
            new Handler<Query<int>>(async (_, _) =>
            {
                intStarted.SetResult();
                await release.Task.WaitAsync(Timeout);
                return "old int";
            }));
        services.AddSingleton<IRequestHandler<Query<string>, string>>(
            new Handler<Query<string>>(async (_, _) =>
            {
                stringStarted.SetResult();
                await release.Task.WaitAsync(Timeout);
                return "old string";
            }));
        using var provider = services.BuildServiceProvider();
        using var queryScope = provider.CreateScope();
        using var updateScope = provider.CreateScope();
        var queryMediator = queryScope.ServiceProvider.GetRequiredService<IMediator>();
        var updateMediator = updateScope.ServiceProvider.GetRequiredService<IMediator>();
        var first = new Query<int>();
        var second = new Query<string>();
        var intQuery = queryMediator.SendAsync<Query<int>, string>(first, default).AsTask();
        var stringQuery = queryMediator.SendAsync<Query<string>, string>(second, default).AsTask();
        await Task.WhenAll(intStarted.Task, stringStarted.Task).WaitAsync(Timeout);
        var update = updateMediator.SendAsync<Update, string>(new Update(), default).AsTask();

        try
        {
            Assert.False(update.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(intQuery, stringQuery, update).WaitAsync(Timeout);
        }

        Assert.Equal("updated", await update);
        var cache = provider.GetRequiredService<MemoryCache>();
        Assert.False(cache.TryGetValue(first.GetCacheEntryKey(), out _));
        Assert.False(cache.TryGetValue(second.GetCacheEntryKey(), out _));
    }

    [Fact]
    public async Task Trigger_ShouldNotInvalidateAnotherProvidersClosedVariants()
    {
        var services = CreateServices();
        using var firstProvider = services.BuildServiceProvider();
        using var secondProvider = services.BuildServiceProvider();
        using var firstScope = firstProvider.CreateScope();
        using var secondScope = secondProvider.CreateScope();
        var firstMediator = firstScope.ServiceProvider.GetRequiredService<IMediator>();
        var secondMediator = secondScope.ServiceProvider.GetRequiredService<IMediator>();
        var query = new Query<int>();
        await firstMediator.SendAsync<Query<int>, string>(query, default);
        await secondMediator.SendAsync<Query<int>, string>(query, default);

        await secondMediator.SendAsync<Update, string>(new Update(), default);

        Assert.Equal("int", firstProvider.GetRequiredService<MemoryCache>().Get<string>(query.GetCacheEntryKey()));
        Assert.False(secondProvider.GetRequiredService<MemoryCache>().TryGetValue(query.GetCacheEntryKey(), out _));
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddMitMediator(typeof(IMediator).Assembly);
        services.AddMitMediatorInMemoryCache(assemblies: [typeof(GenericInvalidationTests).Assembly]);
        services.AddSingleton<IRequestHandler<Query<int>, string>>(
            new Handler<Query<int>>((_, _) => ValueTask.FromResult("int")));
        services.AddSingleton<IRequestHandler<Query<string>, string>>(
            new Handler<Query<string>>((_, _) => ValueTask.FromResult("string")));
        services.AddSingleton<IRequestHandler<OtherQuery<int>, string>>(
            new Handler<OtherQuery<int>>((_, _) => ValueTask.FromResult("other")));
        services.AddSingleton<IRequestHandler<Update, string>>(
            new Handler<Update>((_, _) => ValueTask.FromResult("updated")));
        return services;
    }

    [CacheResponseInMemory(typeof(Update))]
    private sealed class Query<T> : IRequest<string>
    {
        public int Id { get; init; }
    }

    [CacheResponseInMemory]
    private sealed class OtherQuery<T> : IRequest<string>;

    private sealed class Update : IRequest<string>;

    private sealed class Handler<TRequest>(Func<TRequest, CancellationToken, ValueTask<string>> invoke)
        : IRequestHandler<TRequest, string> where TRequest : IRequest<string>
    {
        public ValueTask<string> HandleAsync(TRequest request, CancellationToken cancellationToken) =>
            invoke(request, cancellationToken);
    }
}
