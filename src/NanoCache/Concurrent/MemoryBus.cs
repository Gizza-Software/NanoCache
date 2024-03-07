#define UseChannel
#define UseBlockingCollection_

#if UseChannel
using System.Threading.Channels;
#elif UseBlockingCollection
using System.Collections.Concurrent;
#endif

namespace NanoCache.Concurrent;

public interface IMemoryBus<T>
{
    void Publish(T data);
    Task PublishAsync(T data, CancellationToken ct = default);
    Task ConsumeAsync(Func<T, CancellationToken, Task> func, CancellationToken ct);
}

public class MemoryBus<T> : IMemoryBus<T>
{
#if UseChannel
    private readonly Channel<T> _bus = Channel.CreateUnbounded<T>();

    public void Publish(T data)
    {
        _bus.Writer.TryWrite(data);
    }

    public async Task PublishAsync(T data, CancellationToken ct = default)
    {
        await _bus.Writer.WriteAsync(data, ct);
    }

    public async Task ConsumeAsync(Func<T, CancellationToken, Task> func, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (await _bus.Reader.WaitToReadAsync(ct))
            {
                while (_bus.Reader.TryRead(out var item))
                {
                    await func(item, ct);
                }
            }
        }
    }
#elif UseBlockingCollection
    private readonly BlockingCollection<T> _bus = new BlockingCollection<T>();
    
    public void Publish(T data)
    {
        _bus.TryAdd(data);
    }

    public Task PublishAsync(T data, CancellationToken ct = default)
    {
        return Task.FromResult(_bus.TryAdd(data));
    }

    public async Task ConsumeAsync(Func<T, CancellationToken, Task> func, CancellationToken ct)
    {
        foreach (var item in _bus.GetConsumingEnumerable(ct))
        {
            await func(item, ct);
        }
    }
#endif
}
