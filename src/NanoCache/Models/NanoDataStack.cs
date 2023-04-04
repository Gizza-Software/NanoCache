namespace NanoCache.Models;

internal class NanoDataStackClient
{
    public readonly ConcurrentDictionary<long, NanoRequest> CacheServerRequests = new();
    public readonly ConcurrentDictionary<long, TaskCompletionSource<NanoResponse>> CacheServerResponseCallbacks = new();
    public readonly ConcurrentDictionary<long, DateTime> CacheServerResponseTimeouts = new();
}

internal class NanoDataStackServer
{
    public readonly BlockingCollection<NanoWaitingRequest> ClientRequests = new();
}
