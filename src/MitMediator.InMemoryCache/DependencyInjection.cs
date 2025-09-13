using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator.InMemoryCache.Notifications;

namespace MitMediator.InMemoryCache;

public static class DependencyInjection
{
    /// <summary>
    /// Inject InMemoryCacheBehavior for cache requests responses.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <param name="memoryCacheOptions"><see cref="MemoryCacheOptions"/>.</param>
    /// <param name="assemblies"><see cref="Assembly"/>.</param>
    /// <returns><see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddRequestsInMemoryCache(this IServiceCollection services,  MemoryCacheOptions? memoryCacheOptions = null, params Assembly[]? assemblies)
    {

        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        var clearCacheMatrix = new Dictionary<Type, List<Type>>();
        var types =  assemblies.SelectMany(a => a.GetTypes().Where(t => !t.IsAbstract)).ToArray();
        foreach (var type in types)
        {
            var cacheAttribute = type.GetCustomAttribute<CacheResponseAttribute>();
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

        if (clearCacheMatrix.Count != 0)
        {
            ClearCacheMatrix.ClearCacheMatrixDictionary = clearCacheMatrix
                .Select(c => 
                new KeyValuePair<Type,Type[]>(c.Key, c.Value.ToArray()))
                .ToDictionary();
        }
        
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
        services.AddSingleton<MemoryCache>();
        services
            .AddScoped<INotificationHandler<ClearAllResponsesCacheNotification>, ClearAllResponsesCacheNotificationHandler>()
            .AddScoped<INotificationHandler<ClearResponseCacheForRequestNotification>, ClearResponseCacheForRequestHandler>();
        return services;
    }
}