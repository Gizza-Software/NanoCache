namespace NanoCache;

public class NanoRequest : IDisposable
{
    public long Identifier { get; set; }
    public NanoOperation Operation { get; set; }
    public string Key { get; set; }
    public byte[] Value { get; set; }
    public DistributedCacheEntryOptions Options { get; set; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free any other managed objects here.
            this.Key = null;
            this.Value = null;
            this.Options = null;
        }

        // Free any unmanaged objects here.
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~NanoRequest()
    {
        Dispose(false);
    }
}

internal class NanoPendingRequest : IDisposable
{
    public NanoClient Client { get; set; }
    public NanoRequest Request { get; set; }
    public string ConnectionId { get; set; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free any other managed objects here.
            this.Client = null;
            this.Request?.Dispose();
            this.Request = null;
            this.ConnectionId = null;
        }

        // Free any unmanaged objects here.
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~NanoPendingRequest()
    {
        Dispose(false);
    }
}
