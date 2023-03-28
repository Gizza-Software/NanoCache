namespace NanoCache.Models;

public class NanoRequest
{
    public long Identifier { get; set; }
    public NanoOperation Operation { get; set; }
    public string Key { get; set; }
    public byte[] Value { get; set; }
    public NanoCacheEntryOptions Options { get; set; }
}

public class NanoCacheEntryOptions
{
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
}

internal class NanoWaitingRequest
{
    public NanoClient Client { get; set; }
    public NanoRequest Request { get; set; }
    public string ConnectionId { get; set; }
}
