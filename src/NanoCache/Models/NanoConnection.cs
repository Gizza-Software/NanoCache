namespace NanoCache;

internal class NanoConnection
{
    private long _requestCount;
    public long RequestCount => _requestCount;
    public string ConnectionId { get; set; }

    public NanoConnection() { }
    public NanoConnection(string connectionId)
    {
        ConnectionId = connectionId;
    }

    public long RequestCounter() => Interlocked.Increment(ref _requestCount);
}
