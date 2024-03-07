namespace NanoCache;

public static class NanoCacheExtensions
{
    public static IServiceCollection AddNanoDistributedCache(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.AddOptions();
        services.TryAdd(ServiceDescriptor.Singleton<IDistributedCache, NanoCacheClient>());

        return services;
    }

    public static IServiceCollection AddNanoDistributedCache(this IServiceCollection services, Action<NanoCacheOptions> setupAction)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (setupAction is null) throw new ArgumentNullException(nameof(setupAction));

        services.AddNanoDistributedCache();
        services.Configure(setupAction);

        return services;
    }
}
