#define IPWORKS_
#define TCPSHARP

#if IPWORKS
using nsoftware.IPWorks;
#elif TCPSHARP
using TcpSharp;
#endif

namespace NanoCache;

public sealed class NanoCacheClient : IDistributedCache
{
    // Session
#if IPWORKS
    public bool Connected { get => _ipwclient.Connected; }
#elif TCPSHARP
    public bool Connected { get => _tcpclient.Connected; }
#endif

    // Identifier
    private long _identifier;

    // TCP Socket
#if IPWORKS
    private Tcpclient _ipwclient;
#elif TCPSHARP
    private TcpSharpSocketClient _tcpclient;
#endif
    private List<byte> _buffer = [];

    // Distributed Cache Options
    private NanoCacheOptions _options;

    // Bus
    private readonly IMemoryBus<OnClientDataReceivedEventArgs> _bus = new MemoryBus<OnClientDataReceivedEventArgs>();

    // Data Stack
    private readonly ConcurrentDictionary<long, DateTime> _timeouts = [];
    private readonly ConcurrentDictionary<long, NanoRequest> _requests = [];
    private readonly ConcurrentDictionary<long, TaskCompletionSource<NanoResponse>> _callbacks = [];

    [Obsolete("Do not use constructor directly instead pull IDistributedCache from DI. For this add 'builder.Services.AddNanoDistributedCache'")]
    public NanoCacheClient(IOptions<NanoCacheOptions> options) : this(options.Value)
    {
    }

    public NanoCacheClient(NanoCacheOptions options)
    {
        // Configure
        Configure(options);

        // Timeout Manager
        Task.Factory.StartNew(TimeoutActionAsync, TaskCreationOptions.LongRunning);

        // Memory Bus
        Task.Factory.StartNew(BusConsumerAsync, TaskCreationOptions.LongRunning);

        // Memory Optimizer
        new NanoCacheMemory().Start();
    }

    public void Configure(NanoCacheOptions options)
    {
        // Options
        _options = options;

        // TCP Socket
#if IPWORKS
        _ipwclient = new Tcpclient(NanoCacheConstants.IPWorksRuntimeKey);
        _ipwclient.RemoteHost = _options.CacheServerHost;
        _ipwclient.RemotePort = _options.CacheServerPort;
        _ipwclient.Config("TcpNoDelay=true");
        _ipwclient.Config("InBufferSize=30000000");
        _ipwclient.OnDataIn += new Tcpclient.OnDataInHandler(Client_OnDataReceived);
        _ipwclient.OnReadyToSend += new Tcpclient.OnReadyToSendHandler(Client_OnReadyToSend);
        _ipwclient.OnDisconnected += new Tcpclient.OnDisconnectedHandler(Client_OnDisconnected);
        _ipwclient.KeepAlive = true;
#elif TCPSHARP
        _tcpclient = new TcpSharpSocketClient(_options.CacheServerHost, _options.CacheServerPort);
        _tcpclient.Reconnect = _options.Reconnect;
        _tcpclient.ReconnectDelayInSeconds = _options.ReconnectIntervalInSeconds;
        _tcpclient.NoDelay = true;
        _tcpclient.KeepAlive = true;
        _tcpclient.KeepAliveTime = 900;
        _tcpclient.KeepAliveInterval = 300;
        _tcpclient.KeepAliveRetryCount = 5;
        _tcpclient.OnConnected += Client_OnReadyToSend;
        _tcpclient.OnDisconnected += Client_OnDisconnected;
        _tcpclient.OnDataReceived += Client_OnDataReceived;
#endif
    }

    public void Connection()
    {
        // Check Connection
        if (Connected)
            return;

        // Connect
        Connect();

        // Wait for connection
        var timeout = DateTime.Now.AddSeconds(_options.ConnectionTimeoutInSeconds);
        while (!Connected && DateTime.Now < timeout) Task.Delay(100).GetAwaiter().GetResult();

        // Check Connection
        if (!Connected) throw new InvalidOperationException("Connection timed out");
    }

    public void Connect()
    {
#if IPWORKS
        if (!Connected)
            _ipwclient.Connect();
#elif TCPSHARP
        if (!Connected)
            _tcpclient.Connect();
#endif
    }

    public void Disconnect()
    {
#if IPWORKS
        if (_ipwclient is null || Connected)
            _ipwclient!.Disconnect();
#elif TCPSHARP
        if (_tcpclient is null || Connected)
            _tcpclient!.Disconnect();
#endif
    }

    public void Reconnect()
    {
        // Disconnect
        Disconnect();

        // Wait for disconnection
        var timeout = DateTime.Now.AddSeconds(_options.ConnectionTimeoutInSeconds);
        while (Connected && DateTime.Now < timeout) Task.Delay(100).GetAwaiter().GetResult();

        // Connect
        Connect();
    }

    #region Tasks
    private async Task TimeoutActionAsync()
    {
#if RELEASE
        try
        {
#endif
        while (true)
        {
            var ids = _timeouts.Where(x => x.Value < DateTime.Now).Select(x => x.Key).ToList();
            foreach (var id in ids)
            {
                // Set Response
                if (_callbacks.TryGetValue(id, out var callback))
                    callback.TrySetResult(new NanoResponse
                    {
                        Success = false,
                        Identifier = id,
                        Operation = NanoOperation.Timeout,
                        Value = []
                    });

                // Remove Items
                _timeouts.TryRemove(id, out _);
                _requests.TryRemove(id, out _);
                _callbacks.TryRemove(id, out _);
            }

            // Disconnected?
            // if (_options.Reconnect) Reconnect();

            // Wait for next turn
            await Task.Delay(_options.QueryTimeoutInSeconds < 5 ? 100 : 1000);
        }
#if RELEASE
        }
        finally
        {
            _ = Task.Factory.StartNew(TimeoutActionAsync, TaskCreationOptions.LongRunning);
        }
#endif
    }

    private async Task BusConsumerAsync()
    {
        await _bus.ConsumeAsync(async (e, ct) =>
        {
            SocketHelpers.CacheAndConsume("CLIENT", ref _buffer, e.Data, new Action<byte[], string>(PacketReceived));
            await Task.CompletedTask;
        }, CancellationToken.None);
    }
    #endregion

    #region TCP Socket Events
#if IPWORKS
    private void Client_OnReadyToSend(object sender, TcpclientReadyToSendEventArgs e)
    {
    }

    private void Client_OnDisconnected(object sender, TcpclientDisconnectedEventArgs e)
    {
    }

    private void Client_OnDataReceived(object sender, TcpclientDataInEventArgs e)
    {
        SocketHelpers.CacheAndConsume(e.TextB, "CLIENT", _buffer, new Action<byte[], string>(PacketReceived));
    }
#elif TCPSHARP
    private void Client_OnReadyToSend(object sender, OnClientConnectedEventArgs e)
    {
        // ..
    }

    private void Client_OnDisconnected(object sender, OnClientDisconnectedEventArgs e)
    {
        // ..
    }

    private void Client_OnDataReceived(object sender, OnClientDataReceivedEventArgs e)
    {
        _bus.Publish(e);
    }
#endif

    private void PacketReceived(byte[] bytes, string connectionId)
    {
#if RELEASE
        try
        {
#endif
        // Check Bytes
        if (bytes.Length < 2) return;
        if (bytes[0] < 1 || bytes[0] > 14) return;

        // Parse Bytes
        var dataType = (NanoOperation)bytes[0];
        var dataBody = new byte[bytes.Length - 1];
        Array.Copy(bytes, 1, dataBody, 0, bytes.Length - 1);
        bytes = null;

        // Get Data
        var response = BinaryHelpers.Deserialize<NanoResponse>(dataBody);
        if (response is null) return;
        dataBody = null;

        // Set Value
        if (_callbacks.TryGetValue(response.Identifier, out var callback))
            callback.TrySetResult(response);

        // Remove Items
        _timeouts.TryRemove(response.Identifier, out _);
        _requests.TryRemove(response.Identifier, out _);
        _callbacks.TryRemove(response.Identifier, out _);
#if RELEASE
        }
        catch { }
#endif
    }
    #endregion

    #region TCP Socket Methods
    private NanoResponse Send(NanoOperation operation, string key, byte[] value = null, DistributedCacheEntryOptions options = null)
    {
        return SendAsync(operation, key, value, options, default).GetAwaiter().GetResult();
    }

    private Task<NanoResponse> SendAsync(NanoOperation operation, string key, byte[] value = null, DistributedCacheEntryOptions options = null, CancellationToken token = default)
    {
        // Cancellation Token
        token.ThrowIfCancellationRequested();

        // Prepare Request
        var id = _identifier++;
        var req = new NanoRequest
        {
            Identifier = id,
            Operation = operation,
            Key = key,
            Value = value,
            Options = options,
        };

        // Add to DataStack
        var tcs = new TaskCompletionSource<NanoResponse>();
        _timeouts.TryAdd(id, DateTime.Now.AddSeconds(Connected
        ? _options.QueryTimeoutInSeconds
        : _options.QueryTimeoutInSeconds + _options.ConnectionTimeoutInSeconds));
        _requests.TryAdd(id, req);
        _callbacks.TryAdd(id, tcs);

        // Cancellation Token
        token.Register(() =>
        {
            _timeouts.TryRemove(id, out _);
            _requests.TryRemove(id, out _);
            _callbacks.TryRemove(id, out _);
        });

#if IPWORKS
        // Send
        _ipwclient.SendBytes(req.PrepareObjectToSend());
#elif TCPSHARP
        // Send
        _tcpclient.SendBytesAsync(req.PrepareObjectToSend(), token);
#endif

        // Return
        return tcs.Task;
    }
    #endregion

    #region IDistributedCache Methods
    public byte[] Get(string key)
    {
        return GetAsync(key, default).GetAwaiter().GetResult();
    }
    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        // Cancellation Token
        token.ThrowIfCancellationRequested();

        // Connection
        Connection();

        // Action
        var response = await SendAsync(NanoOperation.Get, key, null, null, token);
        if (response is null || !response.Success) return [];

        // Return
        return response.Value;
    }

    public void Remove(string key)
    {
        RemoveAsync(key, default).GetAwaiter().GetResult();
    }
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        // Cancellation Token
        token.ThrowIfCancellationRequested();

        // Connection
        Connection();

        // Action
        return SendAsync(NanoOperation.Remove, key, null, null, token);
    }

    public void Refresh(string key)
    {
        RefreshAsync(key, default).GetAwaiter().GetResult();
    }
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        // Cancellation Token
        token.ThrowIfCancellationRequested();

        // Connection
        Connection();

        // Action
        return SendAsync(NanoOperation.Refresh, key, null, null, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        SetAsync(key, value, options, default).GetAwaiter().GetResult();
    }
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        // Cancellation Token
        token.ThrowIfCancellationRequested();

        // Connection
        Connection();

        // Action
        return SendAsync(NanoOperation.Set, key, value, options, token);
    }
    #endregion
}