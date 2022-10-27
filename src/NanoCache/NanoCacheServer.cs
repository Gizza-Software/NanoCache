using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using NanoCache.Enums;
using NanoCache.Helpers;
using NanoCache.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TcpSharp;
using TcpSharp.Events.Server;

namespace NanoCache
{
    public sealed class NanoCacheServer
    {
        /* Memory Cache */
        private readonly IMemoryCache _cache;

        /* TCP Socket */
        private readonly TcpSharpSocketServer _listener;
        private readonly ConcurrentDictionary<long, List<byte>> _buffers = new();
        private readonly ConcurrentDictionary<long, NanoClient> _clients = new();

        /* Security */
        private readonly bool _useCredentials;
        private readonly List<NanoUserCredentials> _validUsers;

        /* Debugging */
        private readonly bool _debugMode;

        public NanoCacheServer(IMemoryCache cache, int port, bool useCredentials, List<NanoUserCredentials> validUsers, bool debugMode = false)
        {
            /* Memory Cache */
            _cache = cache;

            /* Security */
            _useCredentials = useCredentials;
            _validUsers = validUsers;

            /* Debugging */
            _debugMode = debugMode;

            /* Query Comsumer Thread */
            Task.Factory.StartNew(QueryConsumer, TaskCreationOptions.LongRunning);

            /* TCP Socket */
            _listener = new TcpSharpSocketServer(port);
            _listener.OnStarted += Server_OnStarted;
            _listener.OnStopped += Server_OnStopped;
            _listener.OnConnected += Server_OnConnected;
            _listener.OnDisconnected += Server_OnDisconnected;
            _listener.OnDataReceived += Server_OnDataReceived;
            _listener.StartListening();
        }

        #region Query Manager
        private void QueryConsumer()
        {
            foreach (var item in NanoDataStack.Server.ClientRequests.GetConsumingEnumerable())
            {
#if RELEASE
                try
                {
#endif
                if (_debugMode)
                {
                    Console.WriteLine("----------------------------------------------------------------------------------------------------");
                    Console.WriteLine("New Request");
                    Console.WriteLine("Client Connection Id: " + item.Client.ConnectionId);
                    Console.WriteLine("Client Logged In    : " + item.Client.LoggedIn);
                    Console.WriteLine("Request Identifier  : " + item.Request.Identifier);
                    Console.WriteLine("Request Operation   : " + item.Request.Operation.ToString());
                    Console.WriteLine("Request Key         : " + item.Request.Key);
                    //var requestData = MessagePackSerializer.Deserialize<NanoUserOptions>(item.Request.Value, NanoConstants.MessagePackOptions);
                    //Console.WriteLine("Request Value       : " + JsonConvert.SerializeObject(requestData));
                    Console.WriteLine("Request Options     : " + JsonConvert.SerializeObject(item.Request.Options));
                }

                if (item == null) continue;
                if (item.Client == null) continue;
                if (item.Request == null) continue;
                var client = _listener.GetClient(item.ConnectionId);
                if (client == null || !client.Connected) continue;

                switch (item.Request.Operation)
                {
                    case NanoOperation.Ping:
                        ResponsePing(item);
                        break;
                    case NanoOperation.Login:
                        ResponseLogin(item);
                        break;
                    case NanoOperation.Logout:
                        ResponseLogout(item);
                        break;
                    case NanoOperation.Set:
                        ResponseSet(item);
                        break;
                    case NanoOperation.Get:
                        ResponseGet(item);
                        break;
                    case NanoOperation.Refresh:
                        ResponseRefresh(item);
                        break;
                    case NanoOperation.Remove:
                        ResponseRemove(item);
                        break;
                }
#if RELEASE
                }
                catch { }
#endif
            }
        }

        private void ResponsePing(NanoWaitingRequest item)
        {
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseLogin(NanoWaitingRequest item)
        {
            var requestData = MessagePackSerializer.Deserialize<NanoUserOptions>(item.Request.Value, NanoConstants.MessagePackOptions);
            if (requestData == null)
            {
                ResponseFailure(item);
                return;
            }

            var validUser = this._validUsers.FirstOrDefault(x => x.Username.Trim() == requestData.Username.Trim() && x.Password.Trim() == requestData.Password.Trim());
            if (validUser == null && this._useCredentials)
            {
                ResponseFailure(item);
                return;
            }

            item.Client.Login(requestData);
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseLogout(NanoWaitingRequest item)
        {
            item.Client.Logout();
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseSet(NanoWaitingRequest item)
        {
            // Check Point
            if (!item.Client.LoggedIn || item.Client.Options == null)
            {
                ResponseFailure(item);
                return;
            }

            // Options
            var options = new MemoryCacheEntryOptions();
            if (item.Client.Options.DefaultAbsoluteExpiration.HasValue) options.AbsoluteExpiration = item.Client.Options.DefaultAbsoluteExpiration.Value;
            if (item.Client.Options.DefaultAbsoluteExpirationRelativeToNow.HasValue) options.AbsoluteExpirationRelativeToNow = item.Client.Options.DefaultAbsoluteExpirationRelativeToNow.Value;
            if (item.Client.Options.DefaultSlidingExpiration.HasValue) options.SlidingExpiration = item.Client.Options.DefaultSlidingExpiration.Value;

            // Override Options
            if (item.Request.Options != null)
            {
                if (item.Request.Options.AbsoluteExpiration.HasValue) options.AbsoluteExpiration = item.Request.Options.AbsoluteExpiration.Value;
                if (item.Request.Options.AbsoluteExpirationRelativeToNow.HasValue) options.AbsoluteExpirationRelativeToNow = item.Request.Options.AbsoluteExpirationRelativeToNow.Value;
                if (item.Request.Options.SlidingExpiration.HasValue) options.SlidingExpiration = item.Request.Options.SlidingExpiration.Value;
            }

            // Action
            this._cache.Set(InstanceKey(item), item.Request.Value, options);

            // Response
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseGet(NanoWaitingRequest item)
        {
            // Check Point
            if (!item.Client.LoggedIn || item.Client.Options == null)
            {
                ResponseFailure(item);
                return;
            }

            // Action
            var data = this._cache.Get<byte[]>(InstanceKey(item));

            // Response
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = data,
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseRefresh(NanoWaitingRequest item)
        {
            // Check Point
            if (!item.Client.LoggedIn || item.Client.Options == null)
            {
                ResponseFailure(item);
                return;
            }

            // Action
            _ = this._cache.Get<byte[]>(InstanceKey(item));

            // Response
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseRemove(NanoWaitingRequest item)
        {
            // Check Point
            if (!item.Client.LoggedIn || item.Client.Options == null)
            {
                ResponseFailure(item);
                return;
            }

            // Action
            this._cache.Remove(InstanceKey(item));

            // Response
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = item.Request.Operation,
                Key = item.Request.Key,
                Value = new byte[1] { 0x01 },
                Success = true,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private void ResponseFailure(NanoWaitingRequest item)
        {
            var response = new NanoResponse
            {
                Identifier = item.Request.Identifier,
                Operation = NanoOperation.Failed,
                Key = item.Request.Key,
                Value = new byte[1] { 0x00 },
                Success = false,
            };
            var bytes = response.PrepareObjectToSend(item.Client.LoggedIn && item.Client.Options != null ? item.Client.Options.UseCompression : false);
            _listener.SendBytes(item.ConnectionId, bytes);
        }

        private string InstanceKey(NanoWaitingRequest item)
        {
            var key =
                string.IsNullOrWhiteSpace(item.Client.Options.Instance)
                ? item.Request.Key
                : item.Client.Options.Instance + "." + item.Request.Key;
            return key;
        }
        #endregion

        #region NanoSocket Methods
        private void Server_OnStarted(object sender, OnStartedEventArgs e)
        {
            Console.WriteLine("The Server has started.");
        }

        private void Server_OnStopped(object sender, OnStoppedEventArgs e)
        {
            Console.WriteLine("The Server has stopped.");
        }

        private void Server_OnConnected(object sender, OnConnectedEventArgs e)
        {
            _clients[e.ConnectionId] = new NanoClient(!_useCredentials, e.ConnectionId);
        }

        private void Server_OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            if (_clients.ContainsKey(e.ConnectionId))
            {
                _clients.TryRemove(e.ConnectionId, out _);
            }
        }

        private void Server_OnDataReceived(object sender, OnDataReceivedEventArgs e)
        {
            var buffer = _buffers.GetOrAdd(e.ConnectionId, new List<byte>());
            SocketHelpers.CacheAndConsume(e.Data, e.ConnectionId, buffer, new Action<byte[], long>(PacketReceived));
        }

        private void PacketReceived(byte[] bytes, long connectionId)
        {
#if RELEASE
            try
            {
#endif
            if (bytes.Length < 2) return;
            if (bytes[0] < 1 || bytes[0] > 14) return;

            var client = _clients[connectionId];
            var dataType = (NanoOperation)bytes[0];
            var dataBody = new byte[bytes.Length - 1];
            Array.Copy(bytes, 1, dataBody, 0, bytes.Length - 1);

            var request = MessagePackSerializer.Deserialize<NanoRequest>(dataBody,
                client.LoggedIn && client.Options != null && client.Options.UseCompression
                ? NanoConstants.MessagePackOptionsWithCompression : NanoConstants.MessagePackOptions);
            if (request == null) return;

            // Add to DataStack
            NanoDataStack.Server.ClientRequests.TryAdd(new NanoWaitingRequest
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
}
