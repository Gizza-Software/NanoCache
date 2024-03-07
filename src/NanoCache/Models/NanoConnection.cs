namespace NanoCache;

internal class NanoConnection
{
    public string ConnectionId { get; set; }

    public NanoConnection() { }
    public NanoConnection(string connectionId)
    {
        ConnectionId = connectionId;
    }
}
