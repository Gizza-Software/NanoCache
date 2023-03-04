namespace NanoCache;

public class NanoCacheOptions
{
    /* Connection */
    public string CacheServerHost { get; set; } = "localhost";
    public int CacheServerPort { get; set; } = 5566;
    public int ConnectionTimeoutInSeconds { get; set; } = 30;
    public bool Ping { get; set; } = true;
    public int PingIntervalInSeconds { get; set; } = 15;
    public bool Reconnect { get; set; } = true;
    public int ReconnectIntervalInSeconds { get; set; } = 10;
    public bool UseCompression { get; set; } = true;
    public int QueryTimeoutInSeconds { get; set; } = 10;

    /* Security */
    public string Username { get; set; }
    public string Password { get; set; }
    public string Instance { get; set; }

    /* Default DistributedCacheEntryOptions */
    public DateTimeOffset? DefaultAbsoluteExpiration { get; set; }
    public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? DefaultSlidingExpiration { get; set; }
}
