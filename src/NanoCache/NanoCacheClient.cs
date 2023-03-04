namespace NanoCache;

public sealed class NanoCacheClient : IDistributedCache
{
    /* Identifier */
    private long _identifier;

    /* Session */
    private bool _loggedIn;

    /* TCP Socket */
    private readonly TcpSharpSocketClient _client;
    private readonly List<byte> _buffer = new();

    /* Distributed Cache Options */
    private readonly NanoCacheOptions _options;

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
        _client.OnConnected += Client_OnConnected;
        _client.OnDisconnected += Client_OnDisconnected;
        _client.OnDataReceived += Client_OnDataReceived;
        _client.Connect();
    }

    #region NanoSocket Events
    private void Client_OnConnected(object sender, TcpSharp.Events.Client.OnConnectedEventArgs e)
    {
        if (_client.Connected)
        {
            var loginModel = new NanoUserOptions
            {
                Username = _options.Username,
                Password = _options.Password,
                Instance = _options.Instance,
                UseCompression = _options.UseCompression,
                DefaultAbsoluteExpiration = _options.DefaultAbsoluteExpiration,
                DefaultAbsoluteExpirationRelativeToNow = _options.DefaultAbsoluteExpirationRelativeToNow,
                DefaultSlidingExpiration = _options.DefaultSlidingExpiration
            };

            var loginResponse =  Send(NanoOperation.Login, "", MessagePackSerializer.Serialize(loginModel, NanoConstants.MessagePackOptions));
            this._loggedIn = loginResponse != null && loginResponse.Success;
        }
    }

    private void Client_OnDisconnected(object sender, TcpSharp.Events.Client.OnDisconnectedEventArgs e)
    {
        this._loggedIn = false;
    }

    private void Client_OnDataReceived(object sender, TcpSharp.Events.Client.OnDataReceivedEventArgs e)
    {
        SocketHelpers.CacheAndConsume(e.Data, 0, _buffer, new Action<byte[], long>((bytes, connectionId) => { PacketReceived(bytes, connectionId); }));
    }

    private void PacketReceived(byte[] bytes, long connectionId)
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
        var response = MessagePackSerializer.Deserialize<NanoResponse>(dataBody, _options.UseCompression ? NanoConstants.MessagePackOptionsWithCompression : NanoConstants.MessagePackOptions);
        if (response == null) return;

        // Set Value
        if (NanoDataStack.Client.CacheServerResponseCallbacks.ContainsKey(response.Identifier))
            NanoDataStack.Client.CacheServerResponseCallbacks[response.Identifier].TrySetResult(response);

        // Remove Request
        if (NanoDataStack.Client.CacheServerRequests.ContainsKey(response.Identifier))
            NanoDataStack.Client.CacheServerRequests.TryRemove(response.Identifier, out _);

        // Remove Timeout
        if (NanoDataStack.Client.CacheServerResponseTimeouts.ContainsKey(response.Identifier))
            NanoDataStack.Client.CacheServerResponseTimeouts.TryRemove(response.Identifier, out _);
#if RELEASE
        }
        catch { }
#endif
    }
    #endregion

    #region NanoSocket Methods
    private NanoResponse Send(NanoOperation operation, string key, byte[] value = null, NanoCacheEntryOptions options = null)
    {
        return SendAsync(operation, key, value, options, default).Result;
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
        NanoDataStack.Client.CacheServerRequests.TryAdd(id, req);
        NanoDataStack.Client.CacheServerResponseCallbacks.TryAdd(id, tcs);
        NanoDataStack.Client.CacheServerResponseTimeouts.TryAdd(id, DateTime.Now.AddSeconds(_options.QueryTimeoutInSeconds));

        // Cancellation Token
        cancellationToken.Register(() =>
        {
            NanoDataStack.Client.CacheServerRequests.TryRemove(id, out _);
            NanoDataStack.Client.CacheServerResponseCallbacks.TryRemove(id, out _);
            NanoDataStack.Client.CacheServerResponseTimeouts.TryRemove(id, out _);
        });

        // Send
        _client.SendBytes(req.PrepareObjectToSend(operation != NanoOperation.Login && _options.UseCompression));

        // Return
        return tcs.Task;
    }
    #endregion

    #region Timeout Methods
    private async Task TimeoutAction()
    {
        while (!_timeoutCancellationToken.IsCancellationRequested)
        {
            var ids = NanoDataStack.Client.CacheServerResponseTimeouts.Where(x => x.Value < DateTime.Now).Select(x => x.Key).ToList();
            foreach (var id in ids)
            {
                // Get Request
                if (NanoDataStack.Client.CacheServerResponseCallbacks.ContainsKey(id))
                    NanoDataStack.Client.CacheServerResponseCallbacks[id].TrySetResult(new NanoResponse
                    {
                        Success = false,
                        Identifier = id,
                        Operation = NanoOperation.Timeout,
                        Value = new byte[0]
                    });

                if (NanoDataStack.Client.CacheServerRequests.ContainsKey(id))
                    NanoDataStack.Client.CacheServerRequests.TryRemove(id, out _);

                if (NanoDataStack.Client.CacheServerResponseTimeouts.ContainsKey(id))
                    NanoDataStack.Client.CacheServerResponseTimeouts.TryRemove(id, out _);
            }

            // Wait for next turn
            await Task.Delay(_options.QueryTimeoutInSeconds < 10 ? 100 : 1000, _timeoutCancellationToken);
        }
    }
    #endregion

    #region IDistributedCache Methods
    public byte[] Get(string key)
    {
        if (!this._loggedIn) return new byte[0];

        var response = Send(NanoOperation.Get, key);
        if (response == null || !response.Success) return new byte[0];

        return response.Value;
    }
    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        if (!this._loggedIn) return new byte[0];

        var response = await SendAsync(NanoOperation.Get, key, null, null, token);
        if (response == null || !response.Success) return new byte[0];

        return response.Value;
    }
    public void Refresh(string key)
    {
        if (!this._loggedIn) return;

        Send(NanoOperation.Refresh, key);
    }
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (!this._loggedIn) return Task.CompletedTask;

        return SendAsync(NanoOperation.Refresh, key, null, null, token);
    }
    public void Remove(string key)
    {
        if (!this._loggedIn) return;

        Send(NanoOperation.Remove, key);
    }
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (!this._loggedIn) return Task.CompletedTask;

        return SendAsync(NanoOperation.Remove, key, null, null, token);
    }
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (!this._loggedIn) return;

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
        if (!this._loggedIn) return Task.CompletedTask;

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