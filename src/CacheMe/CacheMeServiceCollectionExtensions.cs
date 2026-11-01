using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CacheMe;

/// <summary>Registers CacheMe. Each ForType call registers <see cref="ICache{T}"/> as a singleton.</summary>
public static class CacheMeServiceCollectionExtensions
{
    /// <summary>Binds settings from configuration, then applies the builder.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The CacheMe configuration section.</param>
    /// <param name="configure">Registers the store, the policies and any replacements.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCacheMe(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CacheMeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CacheMeOptions>()
            .Bind(configuration, binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();

        return Register(services, configure);
    }

    /// <summary>Takes settings from code only, through <see cref="CacheMeBuilder.Configure"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets the options, and registers the store, the policies and any replacements.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCacheMe(
        this IServiceCollection services,
        Action<CacheMeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CacheMeOptions>().ValidateOnStart();

        return Register(services, configure);
    }

    private static IServiceCollection Register(IServiceCollection services, Action<CacheMeBuilder> configure)
    {
        var builder = new CacheMeBuilder(services);
        configure(builder);
        builder.Complete();
        return services;
    }
}
