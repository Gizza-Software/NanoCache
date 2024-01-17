namespace NanoCache;

public class NanoUserOptions
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Instance { get; set; }
    public DateTimeOffset? DefaultAbsoluteExpiration { get; set; }
    public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? DefaultSlidingExpiration { get; set; }
}
