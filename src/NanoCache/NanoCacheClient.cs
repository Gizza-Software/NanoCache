namespace NanoCache;

public sealed class NanoCacheClient : IDistributedCache
{
    // Session
    public bool Connected { get => _client.Connected; }

    // Identifier
    private long _identifier;

    // TCP Socket
    private TcpSharpSocketClient _client;
    private readonly List<byte> _buffer = [];
    private readonly BlockingCollection<byte[]> _packages = [];

    // Distributed Cache Options
    private NanoCacheOptions _options;

    // Data Stack
    private ConcurrentDictionary<long, DateTime> _timeouts = [];
    private ConcurrentDictionary<long, NanoRequest> _requests = [];
    private ConcurrentDictionary<long, TaskCompletionSource<NanoResponse>> _callbacks = [];

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

        // Memory Optimizer Task
        Task.Factory.StartNew(MemoryOptimizerAsync, TaskCreationOptions.LongRunning);
    }

    public void Configure(NanoCacheOptions options)
    {
        // Options
        _options = options;

        // TCP Socket
        _client = new TcpSharpSocketClient(_options.CacheServerHost, _options.CacheServerPort);
        _client.Reconnect = _options.Reconnect;
        _client.ReconnectDelayInSeconds = _options.ReconnectIntervalInSeconds;
        _client.NoDelay = true;
        _client.KeepAlive = true;
        _client.KeepAliveTime = 900;
        _client.KeepAliveInterval = 300;
        _client.KeepAliveRetryCount = 5;
        _client.OnConnected += Client_OnReadyToSend;
        _client.OnDisconnected += Client_OnDisconnected;
        _client.OnDataReceived += Client_OnDataReceived;
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
        if (!Connected)
            _client.Connect();
    }

    public void Disconnect()
    {
        if (_client == null || Connected)
            _client!.Disconnect();
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

    private async Task MemoryOptimizerAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            GC.Collect();
        }
    }

    #region Timeout Methods
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
    #endregion

    #region TCP Socket Events
    private void Client_OnReadyToSend(object sender, OnClientConnectedEventArgs e)
    {
    }

    private void Client_OnDisconnected(object sender, OnClientDisconnectedEventArgs e)
    {
    }

    private void Client_OnDataReceived(object sender, OnClientDataReceivedEventArgs e)
    {
        SocketHelpers.CacheAndConsume(e.Data, "CLIENT", _buffer, new Action<byte[], string>(PacketReceived));
    }

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
        if (response == null) return;
        dataBody = null;

        // Set Value
        if (_callbacks.TryGetValue(response.Identifier, out var callback))
            callback.TrySetResult(response);

        // Remove Items
        _timeouts.TryRemove(response.Identifier, out _);
        _requests.TryRemove(response.Identifier, out _);
        _callbacks.TryRemove(response.Identifier, out _);
#if RELEASE
        } finally { }
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

        // Send
        _client.SendBytesAsync(req.PrepareObjectToSend(), token);

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
        if (response == null || !response.Success) return [];

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