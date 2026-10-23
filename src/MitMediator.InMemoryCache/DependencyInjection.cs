using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator.InMemoryCache.Notifications;
using MitMediator.InMemoryCache;

namespace MitMediator;

public static class DependencyInjection
{
    /// <summary>
    /// Inject InMemoryCacheBehavior for cache requests responses.
    /// </summary>
    /// <remarks>
    /// Only the first call for a service collection registers services and configures caching.
    /// Subsequent calls leave the collection unchanged and ignore all supplied parameters.
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <param name="memoryCacheOptions"><see cref="MemoryCacheOptions"/>.</param>
    /// <param name="lockStripingGatesCount">Count of lock cache write and invalidate SemaphoresSlim for each request type.</param>
    /// <param name="assemblies"><see cref="Assembly"/>.</param>
    /// <returns><see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddMitMediatorInMemoryCache(
        this IServiceCollection services,  
        MemoryCacheOptions? memoryCacheOptions = null,
        int lockStripingGatesCount = 64,
        params Assembly[]? assemblies)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(InMemoryCacheRegistration)))
        {
            return services;
        }

        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        var clearCacheMatrix = new Dictionary<Type, List<Type>>();
        var types =  assemblies.SelectMany(a => a.GetTypes().Where(t => !t.IsAbstract)).ToArray();
        foreach (var type in types)
        {
            var cacheAttribute = type.GetCustomAttribute<CacheResponseInMemoryAttribute>();
            if (cacheAttribute == null || cacheAttribute.RequestsToClearCache is null)
            {
                continue;
            }
            foreach (var clearCacheTriggerRequest in cacheAttribute.RequestsToClearCache)
            {
                if (!clearCacheMatrix.TryGetValue(clearCacheTriggerRequest, out var toClearList))
                {
                    toClearList = new List<Type>();
                    clearCacheMatrix.Add(clearCacheTriggerRequest, toClearList);
                }
                toClearList.Add(type);
            }
        }

        services.AddSingleton(_ => new ClearCacheMatrix(
            clearCacheMatrix.ToDictionary(c => c.Key, c => c.Value.ToArray())));
        
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InMemoryClearCacheByMatrixBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InMemoryCacheBehavior<,>));
        if (memoryCacheOptions is not null)
        {
            services.AddMemoryCache(options =>
            {
                options.Clock = memoryCacheOptions.Clock;
                options.CompactionPercentage = memoryCacheOptions.CompactionPercentage;
                options.ExpirationScanFrequency = memoryCacheOptions.ExpirationScanFrequency;
                options.SizeLimit = memoryCacheOptions.SizeLimit;
                options.TrackLinkedCacheEntries = memoryCacheOptions.TrackLinkedCacheEntries;
                options.TrackStatistics = memoryCacheOptions.TrackStatistics;
            });
        }
        else
        {
            services.AddMemoryCache();
        }
        services.AddOptions<CacheRequestTypeLocksStripingOptions>()
            .Configure(options => options.LockStripingGatesCount = lockStripingGatesCount)
            .Validate(options => options.LockStripingGatesCount > 0, "LockStripingGatesCount must be greater than zero.");
        services.AddSingleton(typeof(CacheRequestTypeLocksStriping<>), typeof(CacheRequestTypeLocksStriping<>));
        services.AddSingleton<MemoryCache>();
        services.AddSingleton<CacheInvalidationService>();
        services
            .AddScoped<INotificationHandler<ClearAllResponsesCacheNotification>, ClearAllResponsesCacheNotificationHandler>()
            .AddScoped<INotificationHandler<ClearResponseCacheForRequestNotification>, ClearResponseCacheForRequestHandler>();
        services.AddSingleton(new InMemoryCacheRegistration());
        return services;
    }

    private sealed class InMemoryCacheRegistration;
}