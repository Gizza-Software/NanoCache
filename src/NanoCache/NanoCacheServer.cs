#define IPWORKS_
#define TCPSHARP

#if IPWORKS
using nsoftware.IPWorks;
#elif TCPSHARP
using TcpSharp;
#endif

namespace NanoCache;

public sealed class NanoCacheServer
{
    // Memory Cache
    private readonly IMemoryCache _cache;

    // TCP Socket
#if IPWORKS
    private readonly Tcpserver _ipwserver;
#elif TCPSHARP
    private readonly TcpSharpSocketServer _tcpserver;
#endif
    private readonly ConcurrentDictionary<string, List<byte>> _buffers = [];
    private readonly ConcurrentDictionary<string, NanoClient> _clients = [];

    // Data Stack
    private readonly BlockingCollection<NanoPendingRequest> _clientRequests = [];

    // Debugging
    private readonly bool _debugMode;

    public NanoCacheServer(IMemoryCache cache, int port, bool debugMode = false)
    {
        // Memory Cache
        _cache = cache;

        // Debugging
        _debugMode = debugMode;

#if IPWORKS
        // TCP Socket
        _ipwserver = new Tcpserver(NanoCacheConstants.IPWorksRuntimeKey);
        _ipwserver.Config("TcpNoDelay=true");
        _ipwserver.Config("InBufferSize=30000000");
        _ipwserver.OnDataIn += new Tcpserver.OnDataInHandler(Server_OnDataReceived);
        _ipwserver.OnReadyToSend += new Tcpserver.OnReadyToSendHandler(Server_OnConnected);
        _ipwserver.OnDisconnected += new Tcpserver.OnDisconnectedHandler(Server_OnDisconnected);
        _ipwserver.KeepAlive = true;
        _ipwserver.LocalPort = port;
#elif TCPSHARP
        // TCP Socket
        _tcpserver = new TcpSharpSocketServer(port);
        _tcpserver.OnStarted += Server_OnStarted;
        _tcpserver.OnStopped += Server_OnStopped;
        _tcpserver.OnConnected += Server_OnConnected;
        _tcpserver.OnDisconnected += Server_OnDisconnected;
        _tcpserver.OnDataReceived += Server_OnDataReceived;
#endif

        // Query Consumer Task
        Task.Factory.StartNew(ConsumerAsync, TaskCreationOptions.LongRunning);

        // Memory Optimizer Task
        Task.Factory.StartNew(MemoryOptimizerAsync, TaskCreationOptions.LongRunning);
    }

    public void StartListening()
    {
#if IPWORKS
        if (_ipwserver == null || !_ipwserver.Listening)
            _ipwserver!.StartListening();
#elif TCPSHARP
        if (_tcpserver == null || !_tcpserver.Listening)
            _tcpserver!.StartListening();
#endif
    }

    public void StopListening()
    {
#if IPWORKS
        if (_ipwserver == null || _ipwserver.Listening)
            _ipwserver!.StopListening();
#elif TCPSHARP
        if (_tcpserver == null || _tcpserver.Listening)
            _tcpserver!.StopListening();
#endif
    }

    private async Task MemoryOptimizerAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            GC.Collect();
        }
    }

    #region Query Manager
    private async Task ConsumerAsync()
    {
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
            }
#if RELEASE
            }
            finally { }
#endif
        }
    }

    private Task PingAsync(NanoPendingRequest item, CancellationToken token = default)
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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private async Task LoginAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        await Task.CompletedTask;
    }

    private async Task LogoutAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        await Task.CompletedTask;
    }

    private Task SetAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Options
        var options = new MemoryCacheEntryOptions();
        if (item.Request.Options != null)
        {
            options.AbsoluteExpiration = item.Request.Options.AbsoluteExpiration;
            options.AbsoluteExpirationRelativeToNow = item.Request.Options.AbsoluteExpirationRelativeToNow;
            options.SlidingExpiration = item.Request.Options.SlidingExpiration;
        }

        // Action
        this._cache.Set(item.Request.Key, item.Request.Value, options);

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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private Task GetAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Action
        var data = this._cache.Get<byte[]>(item.Request.Key);

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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private Task RefreshAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Action
        _ = this._cache.Get<byte[]>(item.Request.Key);

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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private Task RemoveAsync(NanoPendingRequest item, CancellationToken token = default)
    {
        // Action
        this._cache.Remove(item.Request.Key);

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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }

    private Task FailedAsync(NanoPendingRequest item, CancellationToken token = default)
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
        return SendBytesAsync(item.ConnectionId, bytes, token);
    }
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

#if IPWORKS
    private void Server_OnConnected(object sender, TcpserverReadyToSendEventArgs e)
    {
        _clients[e.ConnectionId] = new NanoClient(e.ConnectionId);
    }

    private void Server_OnDisconnected(object sender, TcpserverDisconnectedEventArgs e)
    {
        _clients.TryRemove(e.ConnectionId, out _);
    }

    private void Server_OnDataReceived(object sender, TcpserverDataInEventArgs e)
    {
        var buffer = _buffers.GetOrAdd(e.ConnectionId, []);
        SocketHelpers.CacheAndConsume(e.TextB, e.ConnectionId, buffer, new Action<byte[], string>(PacketReceived));
    }
#elif TCPSHARP
    private void Server_OnConnected(object sender, OnServerConnectedEventArgs e)
    {
        _clients[e.ConnectionId] = new NanoClient(e.ConnectionId);
    }

    private void Server_OnDisconnected(object sender, OnServerDisconnectedEventArgs e)
    {
        _clients.TryRemove(e.ConnectionId, out _);
    }

    private void Server_OnDataReceived(object sender, OnServerDataReceivedEventArgs e)
    {
        var buffer = _buffers.GetOrAdd(e.ConnectionId, []);
        SocketHelpers.CacheAndConsume(e.Data, e.ConnectionId, buffer, new Action<byte[], string>(PacketReceived));
    }
#endif

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
        if (!_clients.TryGetValue(connectionId, out var client))
            return;

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
        finally { }
#endif
    }

    private void SendBytes(string connectionId, byte[] data)
    {
#if IPWORKS
        _ipwserver.SendBytes(connectionId, data);
#elif TCPSHARP
        _tcpserver.SendBytes(connectionId, data);
#endif
    }

    private Task SendBytesAsync(string connectionId, byte[] data, CancellationToken token = default)
    {
#if IPWORKS
        _ipwserver.SendBytes(connectionId, data);
        return Task.CompletedTask;
#elif TCPSHARP
        return _tcpserver.SendBytesAsync(connectionId, data, token);
#endif
    }
    #endregion

}
