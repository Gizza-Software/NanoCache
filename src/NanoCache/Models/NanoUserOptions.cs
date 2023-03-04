namespace NanoCache.Models;

[MessagePackObject]
public class NanoUserOptions
{
    [Key(0)]
    public string Username { get; set; }

    [Key(1)]
    public string Password { get; set; }

    [Key(2)]
    public string Instance { get; set; }

    [Key(3)]
    public bool UseCompression { get; set; }

    [Key(4)]
    public DateTimeOffset? DefaultAbsoluteExpiration { get; set; }

    [Key(5)]
    public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; }

    [Key(6)]
    public TimeSpan? DefaultSlidingExpiration { get; set; }
}
