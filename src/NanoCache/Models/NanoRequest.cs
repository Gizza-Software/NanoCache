namespace NanoCache;

public class NanoRequest : IDisposable
{
    public long Identifier { get; set; }
    public NanoOperation Operation { get; set; }
    public string Key { get; set; }
    public byte[] Value { get; set; }
    public NanoCacheEntryOptions Options { get; set; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free any other managed objects here.
            this.Key = null;
            this.Value = null;
            this.Options?.Dispose();
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

public class NanoCacheEntryOptions: IDisposable
{
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free any other managed objects here.
            this.AbsoluteExpiration = null;
            this.AbsoluteExpirationRelativeToNow = null;
            this.SlidingExpiration = null;
        }

        // Free any unmanaged objects here.
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~NanoCacheEntryOptions()
    {
        Dispose(false);
    }
}

internal class NanoWaitingRequest: IDisposable
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

    ~NanoWaitingRequest()
    {
        Dispose(false);
    }
}
