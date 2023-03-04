namespace NanoCache.Models;

[MessagePackObject]
public class NanoRequest
{
    [Key(0)]
    public long Identifier { get; set; }

    [Key(1)]
    public NanoOperation Operation { get; set; }

    [Key(2)]
    public string Key { get; set; }

    [Key(3)]
    public byte[] Value { get; set; }

    [Key(4)]
    public NanoCacheEntryOptions Options { get; set; }
}

[MessagePackObject]
public class NanoCacheEntryOptions
{
    [Key(0)]
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    [Key(1)]
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

    [Key(2)]
    public TimeSpan? SlidingExpiration { get; set; }
}

internal class NanoWaitingRequest
{
    public NanoClient Client { get; set; }
    public NanoRequest Request { get; set; }
    public long ConnectionId { get; set; }
}
