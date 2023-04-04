namespace NanoCache;

public sealed class NanoCacheClient : IDistributedCache
{
    /* Session */
    public bool Connected { get => _client.Connected; }
    public bool Authenticated { get; private set; }

    /* Identifier */
    private long _identifier;

    /* TCP Socket */
    private readonly TcpSharpSocketClient _client;
    private readonly List<byte> _buffer = new();

    /* Distributed Cache Options */
    private readonly NanoCacheOptions _options;

    /* Data Stack */
    private NanoDataStackClient _clientDataStack;

    /* Timeout Manager */
    private readonly Thread _timeoutThread;
    private readonly CancellationTokenSource _timeoutCancellationTokenSource;
    private readonly CancellationToken _timeoutCancellationToken;

    [Obsolete("Do not use constructor directly instead pull IDistributedCache from DI. For this add 'builder.Services.AddNanoDistributedCache'")]
    public NanoCacheClient(IOptions<NanoCacheOptions> options) : this(options.Value)
    {
    }

    public NanoCacheClient(NanoCacheOptions options)
    {
        /* Options */
        _options = options;

        // Data Stack
        _clientDataStack = new NanoDataStackClient();

        /* Timeout Manager */
        _timeoutCancellationTokenSource = new CancellationTokenSource();
        _timeoutCancellationToken = _timeoutCancellationTokenSource.Token;
        _timeoutThread = new Thread(async () => await TimeoutAction());
        _timeoutThread.Start();

        /* TCP Socket */
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

    public void Connect()
    {
        if (!Connected)
            _client.Connect();
    }

    public void Disconnect()
    {
        if (_client == null || Connected)
            _client.Disconnect();
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
    }

    public void Authenticate()
    {
        this.Connect();
    }

    #region TCP Socket Events
    private void Client_OnReadyToSend(object sender, OnClientConnectedEventArgs e)
    {
        // Data Stack
        _clientDataStack = new NanoDataStackClient();

        // Login
        if (!this.Authenticated)
        {
            var loginModel = new NanoUserOptions
            {
                Username = _options.Username,
                Password = _options.Password,
                Instance = _options.Instance,
                DefaultAbsoluteExpiration = _options.DefaultAbsoluteExpiration,
                DefaultAbsoluteExpirationRelativeToNow = _options.DefaultAbsoluteExpirationRelativeToNow,
                DefaultSlidingExpiration = _options.DefaultSlidingExpiration
            };

            var loginResponse = Send(NanoOperation.Login, "", BinaryHelpers.Serialize(loginModel));
            this.Authenticated = loginResponse != null && loginResponse.Success;
        }
    }

    private void Client_OnDisconnected(object sender, OnClientDisconnectedEventArgs e)
    {
        this.Authenticated = false;
    }

    private void Client_OnDataReceived(object sender, OnClientDataReceivedEventArgs e)
    {
        SocketHelpers.CacheAndConsume(e.Data, "00000", _buffer, new Action<byte[], string>((bytes, connectionId) => { PacketReceived(bytes, connectionId); }));
    }

    private void PacketReceived(byte[] bytes, string connectionId)
    {
#if RELEASE
        try
        {
#endif
        if (bytes.Length < 2) return;
        if (bytes[0] < 1 || bytes[0] > 14) return;

        // Parse Bytes
        var dataType = (NanoOperation)bytes[0];
        var dataBody = new byte[bytes.Length - 1];
        Array.Copy(bytes, 1, dataBody, 0, bytes.Length - 1);

        // Get Data
        var response = BinaryHelpers.Deserialize<NanoResponse>(dataBody);
        if (response == null) return;

        // Set Value
        if (_clientDataStack.CacheServerResponseCallbacks.ContainsKey(response.Identifier))
            _clientDataStack.CacheServerResponseCallbacks[response.Identifier].TrySetResult(response);

        // Remove Request
        if (_clientDataStack.CacheServerRequests.ContainsKey(response.Identifier))
            _clientDataStack.CacheServerRequests.TryRemove(response.Identifier, out _);

        // Remove Timeout
        if (_clientDataStack.CacheServerResponseTimeouts.ContainsKey(response.Identifier))
            _clientDataStack.CacheServerResponseTimeouts.TryRemove(response.Identifier, out _);
#if RELEASE
        }
        catch { }
#endif
    }
    #endregion

    #region TCP Socket Methods
    private NanoResponse Send(NanoOperation operation, string key, byte[] value = null, NanoCacheEntryOptions options = null)
    {
        return SendAsync(operation, key, value, options, default).GetAwaiter().GetResult();
    }

    private Task<NanoResponse> SendAsync(NanoOperation operation, string key, byte[] value = null, NanoCacheEntryOptions options = null, CancellationToken cancellationToken = default)
    {
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
        _clientDataStack.CacheServerRequests.TryAdd(id, req);
        _clientDataStack.CacheServerResponseCallbacks.TryAdd(id, tcs);
        _clientDataStack.CacheServerResponseTimeouts.TryAdd(id, DateTime.Now.AddSeconds(_options.QueryTimeoutInSeconds));

        // Cancellation Token
        cancellationToken.Register(() =>
        {
            _clientDataStack.CacheServerRequests.TryRemove(id, out _);
            _clientDataStack.CacheServerResponseCallbacks.TryRemove(id, out _);
            _clientDataStack.CacheServerResponseTimeouts.TryRemove(id, out _);
        });

        // Send
        _client.SendBytes(req.PrepareObjectToSend());

        // Return
        return tcs.Task;
    }
    #endregion

    #region Timeout Methods
    private async Task TimeoutAction()
    {
        while (!_timeoutCancellationToken.IsCancellationRequested)
        {
            var ids = _clientDataStack.CacheServerResponseTimeouts.Where(x => x.Value < DateTime.Now).Select(x => x.Key).ToList();
            foreach (var id in ids)
            {
                // Get Request
                if (_clientDataStack.CacheServerResponseCallbacks.ContainsKey(id))
                    _clientDataStack.CacheServerResponseCallbacks[id].TrySetResult(new NanoResponse
                    {
                        Success = false,
                        Identifier = id,
                        Operation = NanoOperation.Timeout,
                        Value = new byte[0]
                    });

                if (_clientDataStack.CacheServerRequests.ContainsKey(id))
                    _clientDataStack.CacheServerRequests.TryRemove(id, out _);

                if (_clientDataStack.CacheServerResponseTimeouts.ContainsKey(id))
                    _clientDataStack.CacheServerResponseTimeouts.TryRemove(id, out _);
            }

            // Disconnected?
            // if (_options.Reconnect) Reconnect();

            // Wait for next turn
            await Task.Delay(_options.QueryTimeoutInSeconds < 5 ? 100 : 1000, _timeoutCancellationToken);
        }
    }
    #endregion

    #region IDistributedCache Methods
    public byte[] Get(string key)
    {
        Authenticate();
        if (!this.Authenticated) return Array.Empty<byte>();

        var response = Send(NanoOperation.Get, key);
        if (response == null || !response.Success) return new byte[0];

        return response.Value;
    }
    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        Authenticate();
        if (!this.Authenticated) return Array.Empty<byte>();

        var response = await SendAsync(NanoOperation.Get, key, null, null, token);
        if (response == null || !response.Success) return new byte[0];

        return response.Value;
    }

    public void Refresh(string key)
    {
        Authenticate();
        if (!this.Authenticated) return;

        Send(NanoOperation.Refresh, key);
    }
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Authenticate();
        if (!this.Authenticated) return Task.CompletedTask;

        return SendAsync(NanoOperation.Refresh, key, null, null, token);
    }

    public void Remove(string key)
    {
        Authenticate();
        if (!this.Authenticated) return;

        Send(NanoOperation.Remove, key);
    }
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Authenticate();
        if (!this.Authenticated) return Task.CompletedTask;

        return SendAsync(NanoOperation.Remove, key, null, null, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        Authenticate();
        if (!this.Authenticated) return;

        var nanoOptions = new NanoCacheEntryOptions();
        if (options != null)
        {
            nanoOptions.SlidingExpiration = options.SlidingExpiration;
            nanoOptions.AbsoluteExpiration = options.AbsoluteExpiration;
            nanoOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
        }

        Send(NanoOperation.Set, key, value, nanoOptions);
    }
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Authenticate();
        if (!this.Authenticated) return Task.CompletedTask;

        var nanoOptions = new NanoCacheEntryOptions();
        if (options != null)
        {
            nanoOptions.SlidingExpiration = options.SlidingExpiration;
            nanoOptions.AbsoluteExpiration = options.AbsoluteExpiration;
            nanoOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
        }

        return SendAsync(NanoOperation.Set, key, value, nanoOptions, token);
    }
    #endregion

}