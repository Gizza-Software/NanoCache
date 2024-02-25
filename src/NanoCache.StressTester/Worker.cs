using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using System.Diagnostics;

namespace NanoCache.StressTester;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly List<Thread> _threads = [];

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        for (int i = 0; i < 1; i++)
        {
            /** /
            var thread = new Thread(i % 2 == 0 ? SyncAction : AsyncAction);
            thread.Name = (i % 2 == 0 ? "SyncThread #" : "AsyncThread #") + i.ToString().PadLeft(2, '0');
            _threads.Add(thread);
            /**/

            /** /
            var thread = new Thread(SyncAction);
            thread.Name = "SyncThread #" + i;
            _threads.Add(thread);
            /**/

            /**/
            var thread = new Thread(AsyncAction);
            thread.Name = "AsyncThread #" + i;
            _threads.Add(thread);
            /**/
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rnd = new Random();
        foreach (var thread in _threads)
        {
            thread.Start();
        }
    }

    protected void AsyncAction()
    {
        /**/
        var client = new NanoCacheClient(new NanoCacheOptions
        {
            CacheServerHost = "localhost",
            CacheServerPort = 5566,
        });
        /**/

        /*
        var client = new RedisCache(new RedisCacheOptions
        {
            InstanceName = "Stress.",
            ConfigurationOptions = new ConfigurationOptions
            {
                Ssl = false,
                User = "",
                Password = "",
                EndPoints = { $"127.0.0.1:6379" },

                ConnectRetry = 5,
                DefaultDatabase = 0,
                ReconnectRetryPolicy = new LinearRetry(5000),
            }
        });
        */

        var rnd = new Random();
        for (var i = 0; i < 1000; i++)
        {
            var sw = Stopwatch.StartNew();
            var data = new byte[rnd.Next(1000, 10000)];
            for (var j = 0; j < 1000; j++)
            {
                _ = client.SetAsync("Key-" + rnd.Next(10, 99), data, new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());
                _ = client.GetAsync("Key-" + rnd.Next(10, 99));
            }
            sw.Stop();
            Console.WriteLine($"[{Thread.CurrentThread.Name} / {i.ToString().PadLeft(4, '0')}] Cache Set/Get done with {data.Length} bytes in {sw.Elapsed}");
        }
    }

    protected void SyncAction()
    {
        /*
        var client = new NanoCacheClient(new NanoCacheOptions
        {
            CacheServerHost = "localhost",
            CacheServerPort = 5566,
        });
        */

        var client = new RedisCache(new RedisCacheOptions
        {
            InstanceName = "Stress.",
            ConfigurationOptions = new ConfigurationOptions
            {
                Ssl = false,
                User = "",
                Password = "",
                EndPoints = { $"127.0.0.1:6379" },

                ConnectRetry = 5,
                DefaultDatabase = 0,
                ReconnectRetryPolicy = new LinearRetry(5000),
            }
        });

        var rnd = new Random();
        for (var i = 0; i < 1000; i++)
        {
            var sw = Stopwatch.StartNew();
            var data = new byte[rnd.Next(1000, 10000)];
            for (var j = 0; j < 1000; j++)
            {
                client.Set("Key-" + rnd.Next(10, 99), data, new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());
                client.Get("Key-" + rnd.Next(10, 99));
            }
            sw.Stop();
            Console.WriteLine($"[{Thread.CurrentThread.Name} / {i.ToString().PadLeft(4, '0')}] Cache Set/Get done with {data.Length} bytes in {sw.Elapsed}");
        }
    }
}
