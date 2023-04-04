namespace NanoCache.Extensions;

public static class DistributedCacheExtensions
{
    public static T GetObject<T>(this IDistributedCache cache, string key)
    {
        byte[] data = cache.Get(key);
        if (data == null) return default;

        return BinaryHelpers.Deserialize<T>(data);
    }

    public static async Task<T> GetObjectAsync<T>(this IDistributedCache cache, string key, CancellationToken token = default)
    {
        byte[] data = await cache.GetAsync(key, token).ConfigureAwait(false);
        if (data == null) return default;

        return BinaryHelpers.Deserialize<T>(data);
    }

    public static void SetObject(this IDistributedCache cache, string key, object value)
    {
        cache.SetObject(key, value, new DistributedCacheEntryOptions());
    }

    public static void SetObject(this IDistributedCache cache, string key, object value, DistributedCacheEntryOptions options)
    {
        if(key == null) throw new ArgumentNullException(nameof(key));
        if(value == null) throw new ArgumentNullException(nameof(value));

        cache.Set(key, BinaryHelpers.Serialize(value), options);
    }

    public static async Task SetObjectAsync(this IDistributedCache cache, string key, object value, CancellationToken token = default)
    {
        await cache.SetObjectAsync(key, value, new DistributedCacheEntryOptions(), token).ConfigureAwait(false);
    }

    public static async Task SetObjectAsync(this IDistributedCache cache, string key, object value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        await cache.SetAsync(key, BinaryHelpers.Serialize(value), options, token).ConfigureAwait(false);
    }

}
