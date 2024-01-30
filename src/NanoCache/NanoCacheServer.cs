namespace NanoCache;

public sealed class NanoCacheServer
{
    // Memory Cache
    private readonly IMemoryCache _cache;

    // TCP Socket
    // private readonly Tcpserver _listener;
    private readonly TcpSharpSocketServer _listener;
    private readonly ConcurrentDictionary<string, List<byte>> _buffers = [];
    private readonly ConcurrentDictionary<string, NanoClient> _clients = [];

    // Security
    private readonly bool _useCredentials;
    private readonly List<NanoUserCredentials> _validUsers;

    // Data Stack
    private readonly BlockingCollection<NanoPendingRequest> _clientRequests = [];

    // Debugging
    private readonly bool _debugMode;

    public NanoCacheServer(IMemoryCache cache, int port, bool useCredentials, List<NanoUserCredentials> validUsers, bool debugMode = false)
    {
        // Memory Cache
        _cache = cache;

        // Security
        _useCredentials = useCredentials;
        _validUsers = validUsers;

        // Debugging
        _debugMode = debugMode;

        /* TCP Socket
        _listener = new Tcpserver(license.RuntimeKey);
        _listener.Config("TcpNoDelay=true");
        _listener.Config("InBufferSize=30000000");
        _listener.OnDataIn += new Tcpserver.OnDataInHandler(Server_OnDataReceived);
        _listener.OnReadyToSend += new Tcpserver.OnReadyToSendHandler(Server_OnConnected);
        _listener.OnDisconnected += new Tcpserver.OnDisconnectedHandler(Server_OnDisconnected);
        _listener.KeepAlive = true;
        _listener.LocalPort = port;
        */

        // TCP Socket
        _listener = new TcpSharpSocketServer(port);
        _listener.OnStarted += Server_OnStarted;
        _listener.OnStopped += Server_OnStopped;
        _listener.OnConnected += Server_OnConnected;
        _listener.OnDisconnected += Server_OnDisconnected;
        _listener.OnDataReceived += Server_OnDataReceived;

        // Query Consumer Task
        Task.Factory.StartNew(ConsumerAsync, TaskCreationOptions.LongRunning);

        // Memory Optimizer Task
        Task.Factory.StartNew(MemoryOptimizerAsync, TaskCreationOptions.LongRunning);
    }

    public void StartListening()
    {
        if (_listener == null || !_listener.Listening)
            _listener!.StartListening();
    }

    public void StopListening()
    {
        if (_listener == null || _listener.Listening)
            _listener!.StopListening();
    }

    DateTime gctime = DateTime.Now;
    private async Task MemoryOptimizerAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            gctime = DateTime.Now;
            GC.Collect();
        }
    }

    #region Query Manager
    private async Task ConsumerAsync()
    {
        await Task.CompletedTask;

#if RELEASE
        try
        {
#endif
            foreach (var item in _clientRequests.GetConsumingEnumerable())
            {
#if RELEASE
                try
                {
#endif
                    using (item)
                    {
                        if (_debugMode)
                        {
                            Console.WriteLine("----------------------------------------------------------------------------------------------------");
                            Console.WriteLine("Client Connection Id: " + item.Client.ConnectionId);
                            Console.WriteLine("Client Logged In    : " + item.Client.LoggedIn);
                            Console.WriteLine("Request Identifier  : " + item.Request.Identifier);
                            Console.WriteLine("Request Operation   : " + item.Request.Operation.ToString());
                            Console.WriteLine("Request Key         : " + item.Request.Key);
                        }

                        if (item == null) continue;
                        if (item.Client == null) continue;
                        if (item.Request == null) continue;

                        Task task = null;
                        var cts = new CancellationTokenSource();
                        switch (item.Request.Operation)
                        {
                            case NanoOperation.Ping:
                                task = PingAsync(item, cts.Token);
                                break;
                            case NanoOperation.Login:
                                task = LoginAsync(item, cts.Token);
                                break;
                            case NanoOperation.Logout:
                                task = LogoutAsync(item, cts.Token);
                                break;
                            case NanoOperation.Set:
                                task = SetAsync(item, cts.Token);
                                break;
                            case NanoOperation.Get:
                                task = GetAsync(item, cts.Token);
                                break;
                            case NanoOperation.Refresh:
                                task = RefreshAsync(item, cts.Token);
                                break;
                            case NanoOperation.Remove:
                                task = RemoveAsync(item, cts.Token);
                                break;
                        }

                        var timeout = Task.Delay(5000, cts.Token);
                        var winner = await Task.WhenAny(task, timeout);
                        if (winner == timeout) cts.Cancel();
                        // else cts.Cancel();
                    }

                    // Memory Optimizer Task
                    if (DateTime.Now.Subtract(gctime).TotalMinutes > 5)
                    {
                        _ = Task.Factory.StartNew(MemoryOptimizerAsync, TaskCreationOptions.LongRunning);
                    }
#if RELEASE
                }
                catch { }
#endif
            }
#if RELEASE
        }
        finally
        {
            _ = Task.Factory.StartNew(ConsumerAsync, TaskCreationOptions.LongRunning);
        }
#endif
    }

    private async Task PingAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task LoginAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        var requestData = BinaryHelpers.Deserialize<NanoUserOptions>(item.Request.Value);
        if (requestData == null)
        {
            await FailureAsync(item);
            return;
        }

        var validUser = this._validUsers.FirstOrDefault(x => x.Username.Trim() == requestData.Username.Trim() && x.Password.Trim() == requestData.Password.Trim());
        if (validUser == null && this._useCredentials)
        {
            await FailureAsync(item);
            return;
        }

        item.Client.Login(requestData);
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task LogoutAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        item.Client.Logout();
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task SetAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Check Point
        if (!item.Client.LoggedIn || item.Client.Options == null)
        {
            await FailureAsync(item);
            return;
        }

        // Options
        var options = new MemoryCacheEntryOptions();
        if (item.Client.Options != null)
        {
            if (item.Client.Options.DefaultAbsoluteExpiration.HasValue) options.AbsoluteExpiration = item.Client.Options.DefaultAbsoluteExpiration.Value;
            if (item.Client.Options.DefaultAbsoluteExpirationRelativeToNow.HasValue) options.AbsoluteExpirationRelativeToNow = item.Client.Options.DefaultAbsoluteExpirationRelativeToNow.Value;
            if (item.Client.Options.DefaultSlidingExpiration.HasValue) options.SlidingExpiration = item.Client.Options.DefaultSlidingExpiration.Value;
        }

        // Override Options
        if (item.Request.Options != null)
        {
            if (item.Request.Options.AbsoluteExpiration.HasValue) options.AbsoluteExpiration = item.Request.Options.AbsoluteExpiration.Value;
            if (item.Request.Options.AbsoluteExpirationRelativeToNow.HasValue) options.AbsoluteExpirationRelativeToNow = item.Request.Options.AbsoluteExpirationRelativeToNow.Value;
            if (item.Request.Options.SlidingExpiration.HasValue) options.SlidingExpiration = item.Request.Options.SlidingExpiration.Value;
        }

        // Action
        // var json = Encoding.UTF8.GetString(item.Request.Value);
        this._cache.Set(Key(item), item.Request.Value, options);

        // Response
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task GetAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Check Point
        if (!item.Client.LoggedIn || item.Client.Options == null)
        {
            await FailureAsync(item);
            return;
        }

        // Action
        var data = this._cache.Get<byte[]>(Key(item));

        // Response
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = data,
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task RefreshAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Check Point
        if (!item.Client.LoggedIn || item.Client.Options == null)
        {
            await FailureAsync(item);
            return;
        }

        // Action
        _ = this._cache.Get<byte[]>(Key(item));

        // Response
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task RemoveAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Check Point
        if (!item.Client.LoggedIn || item.Client.Options == null)
        {
            await FailureAsync(item);
            return;
        }

        // Action
        this._cache.Remove(Key(item));

        // Response
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = item.Request.Operation,
            Key = item.Request.Key,
            Value = [0x01],
            Success = true,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task FailureAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        var response = new NanoResponse
        {
            Identifier = item.Request.Identifier,
            Operation = NanoOperation.Failed,
            Key = item.Request.Key,
            Value = [0x00],
            Success = false,
        };
        var bytes = response.PrepareObjectToSend();
        await _listener.SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private string Key(NanoPendingRequest item)
    => string.IsNullOrWhiteSpace(item.Client.Options.Instance)
        ? item.Request.Key
        : item.Client.Options.Instance.TrimEnd('.') + "." + item.Request.Key;
    #endregion

    #region TCP Socket Methods
    private void Server_OnStarted(object sender, OnServerStartedEventArgs e)
    {
        Console.WriteLine("The Server has started.");
    }

    private void Server_OnStopped(object sender, OnServerStoppedEventArgs e)
    {
        Console.WriteLine("The Server has stopped.");
    }

    // private void Server_OnConnected(object sender, TcpserverReadyToSendEventArgs e)
    private void Server_OnConnected(object sender, OnServerConnectedEventArgs e)
    {
        _clients[e.ConnectionId] = new NanoClient(!_useCredentials, e.ConnectionId.ToString());
    }

    // private void Server_OnDisconnected(object sender, TcpserverDisconnectedEventArgs e)
    private void Server_OnDisconnected(object sender, OnServerDisconnectedEventArgs e)
    {
        _clients.TryRemove(e.ConnectionId, out _);
    }

    // private void Server_OnDataReceived(object sender, TcpserverDataInEventArgs e)
    private void Server_OnDataReceived(object sender, OnServerDataReceivedEventArgs e)
    {
        var buffer = _buffers.GetOrAdd(e.ConnectionId, []);
        SocketHelpers.CacheAndConsume(e.Data, e.ConnectionId, buffer, new Action<byte[], string>(PacketReceived));
    }

    private void PacketReceived(byte[] bytes, string connectionId)
    {
#if RELEASE
        try
        {
#endif
            if (bytes.Length < 2)
                return;
            if (bytes[0] < 1 || bytes[0] > 14)
                return;

            var client = _clients[connectionId];
            var dataType = (NanoOperation)bytes[0];
            var dataBody = new byte[bytes.Length - 1];
            Array.Copy(bytes, 1, dataBody, 0, bytes.Length - 1);

            var request = BinaryHelpers.Deserialize<NanoRequest>(dataBody);
            if (request == null)
                return;

            // Add to DataStack
            _clientRequests.TryAdd(new NanoPendingRequest
            {
                Client = client,
                Request = request,
                ConnectionId = connectionId,
            });
#if RELEASE
        }
        catch { }
#endif
    }
    #endregion

}
