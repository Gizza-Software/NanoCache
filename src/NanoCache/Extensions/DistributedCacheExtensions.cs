namespace NanoCache;

public static class DistributedCacheExtensions
{
    public static T GetObject<T>(this IDistributedCache cache, string key)
    {
        byte[] data = cache.Get(key);
        if (data == null) return default;

        return BinaryHelpers.Deserialize<T>(data);
    }

    public static async Task<T> GetObjectAsync<T>(this IDistributedCache cache, string key)
    {
        byte[] data = await cache.GetAsync(key).ConfigureAwait(false);
        if (data == null) return default;

        return BinaryHelpers.Deserialize<T>(data);
    }

    public static void SetObject(this IDistributedCache cache, string key, object value)
    {
        cache.SetObject(key, value, new DistributedCacheEntryOptions());
    }

    public static void SetObject(this IDistributedCache cache, string key, object value, DistributedCacheEntryOptions options)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value == null) return;

        cache.Set(key, BinaryHelpers.Serialize(value), options);
    }

    public static async Task SetObjectAsync(this IDistributedCache cache, string key, object value)
    {
        await cache.SetObjectAsync(key, value, new DistributedCacheEntryOptions()).ConfigureAwait(false);
    }

    public static async Task SetObjectAsync(this IDistributedCache cache, string key, object value, DistributedCacheEntryOptions options)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value == null) return;

        await cache.SetAsync(key, BinaryHelpers.Serialize(value), options).ConfigureAwait(false);
    }

}
