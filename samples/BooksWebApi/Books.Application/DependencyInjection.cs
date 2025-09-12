using System.Reflection;
using Books.Application.Behaviors;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application;

/// <summary>
/// Dependency injection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Add application services.
    /// </summary>
    /// <param name="services"><see cref="IServiceProvider"/>.</param>
    /// <returns><see cref="IServiceProvider"/></returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        return services
            .AddMitMediator()
            .AddRequestsInMemoryCache(new MemoryCacheOptions { SizeLimit = 1000 })
            .AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true)
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    }
}