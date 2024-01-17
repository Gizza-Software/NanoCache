namespace NanoCache;

internal class NanoDataStackClient
{
    public readonly ConcurrentDictionary<long, NanoRequest> CacheServerRequests = [];
    public readonly ConcurrentDictionary<long, TaskCompletionSource<NanoResponse>> CacheServerResponseCallbacks = [];
    public readonly ConcurrentDictionary<long, DateTime> CacheServerResponseTimeouts = [];
}

internal class NanoDataStackServer
{
    public readonly BlockingCollection<NanoWaitingRequest> ClientRequests = [];
}
