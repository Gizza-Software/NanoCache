namespace NanoCache;

internal class NanoClient
{
    public string ConnectionId { get; set; }

    public NanoClient() { }
    public NanoClient(string connectionId)
    {
        ConnectionId = connectionId;
    }
}
