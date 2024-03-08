namespace NanoCache;

public class NanoCacheMemory
{
    private CancellationTokenSource _cts;
    private CancellationToken _ct;

    public void Start()
    {
        this._cts = new CancellationTokenSource();
        this._ct = _cts.Token;
        _ = Task.Factory.StartNew(CollectGarbageAsync, TaskCreationOptions.LongRunning);
    }

    private async Task CollectGarbageAsync()
    {
#if RELEASE
        try
        {
#endif
        while (!_ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            GC.Collect();
        }
#if RELEASE
        }
        finally
        {
            // Restart
            if (!_ct.IsCancellationRequested)
                this.Start();
        }
#endif
    }
}