namespace NanoCache;

internal class NanoConnection
{
    private long _requestCount;
    public long RequestCount => _requestCount;
    public string ConnectionId { get; set; }
    public DateTime ConnectionAt { get; }

    public NanoConnection(): this(string.Empty) {}
    public NanoConnection(string connectionId)
    {
        ConnectionAt = DateTime.Now;
        ConnectionId = connectionId;
    }

    public long RequestCounter() => Interlocked.Increment(ref _requestCount);
}
