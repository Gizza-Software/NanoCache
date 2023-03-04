namespace NanoCache.Helpers;

internal static class MemoryCacheExtensions
{
#if NETCOREAPP1_0_OR_GREATER
    private static readonly Func<MemoryCache, object> GetEntriesCollection =
        Delegate.CreateDelegate(
        typeof(Func<MemoryCache, object>),
        typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true),
        throwOnBindFailure: true) as Func<MemoryCache, object>;

    public static IDictionary GetCollection(this IMemoryCache memoryCache) =>
        ((IDictionary)GetEntriesCollection((MemoryCache)memoryCache));

    public static IEnumerable GetKeys(this IMemoryCache memoryCache) =>
        ((IDictionary)GetEntriesCollection((MemoryCache)memoryCache)).Keys;

    public static IEnumerable GetValues(this IMemoryCache memoryCache) =>
        ((IDictionary)GetEntriesCollection((MemoryCache)memoryCache)).Values;

    public static int GetKeysCount(this IMemoryCache memoryCache) =>
        ((MemoryCache)memoryCache).Count;
#endif
}
