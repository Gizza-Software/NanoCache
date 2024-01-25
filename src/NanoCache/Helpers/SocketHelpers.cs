namespace NanoCache;

public enum SocketSecurity
{
    None = 0,
    CRC16 = 1,
    CRC32 = 2,
}

internal static class SocketHelpers
{
    private static readonly object _lock = new();

    public static void CacheAndConsume(byte[] bytes, string connectionId, List<byte> buffer, Action<byte[], string> consumer)
    {
        var security = SocketSecurity.None;
        var header = new byte[] { 0xF1, 0xF2 };

#if RELEASE
        try
        {
#endif
        // Gelen verileri buffer'a ekle ve bu halini "buff" olarak al. Sonrasında bufferı temizle
        lock (_lock)
        {
            buffer.AddRange(bytes);
        }

        // Minimum paket uzunluğu 8 byte
        // * SYNC     : 2 Bytes
        // * Length   : 4 Bytes
        // * Data Type: 1 Byte
        // * Content  : 1 Byte(Minimum)
        // * CRC16    : 2 Bytes
        // * CRC32    : 4 Bytes

        var crcLength = 0;
        var syncLength = header.Length;
        var lengthLength = 4;
        var dataTypeLength = 1;
        var minimumDataLength = 1;
        var minimumPacketLength = syncLength + lengthLength + dataTypeLength + crcLength + minimumDataLength;
        if (security == SocketSecurity.CRC16) crcLength = 2;
        else if (security == SocketSecurity.CRC32) crcLength = 4;

        var bufferLength = 0;
        var bufferIndexOf = -1;
        lock (_lock)
        {
            bufferLength = buffer.Count;
            bufferIndexOf = buffer.IndexOf(header);
        }

        if (bufferLength >= minimumPacketLength)
        {
            if (bufferIndexOf == 0) // SYNC Bytes
            {
                // lenghtValue = Data Type (1) + Content (X)
                // lenghtValue CRC bytelarını kapsamıyor.
                var lenghtBytes = buffer.Skip(syncLength).Take(4).ToArray();
                var lengthValue = BitConverter.ToInt32(lenghtBytes, 0);
                var packetLength = syncLength + lengthLength + lengthValue + crcLength;
                var preBytesLength = syncLength + lengthLength;
                if (bufferLength >= packetLength)
                {
#if RELEASE
                        try
                        {
#endif
                    // Security
                    var consume = false;
                    byte[] payload = null;
                    lock (_lock)
                    {
                        if (security == SocketSecurity.None)
                        {
                            payload = buffer.Skip(preBytesLength).Take(lengthValue).ToArray();
                            consume = true;
                        }
                        else if (security == SocketSecurity.CRC16)
                        {
                            var crcBytes = buffer.Skip(lengthValue + preBytesLength).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                            payload = buffer.Skip(preBytesLength).Take(lengthValue).ToArray();
                            consume = CRC16.CheckChecksum(payload, crcValue);
                        }
                        else if (security == SocketSecurity.CRC32)
                        {
                            var crcBytes = buffer.Skip(lengthValue + preBytesLength).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt32(crcBytes, 0);
                            payload = buffer.Skip(preBytesLength).Take(lengthValue).ToArray();
                            consume = CRC32.CheckChecksum(payload, crcValue);
                        }
                    }

                    // Remove from Buffer
                    lock (_lock)
                    {
                        buffer.RemoveRange(0, packetLength);
                    }

                    // Consume
                    if (consume)
                    {
                        consumer(payload, connectionId);
                    }
#if RELEASE
                        }
                        catch { }
#endif
                }
            }
            else if (bufferIndexOf == -1)
            {
                lock (_lock)
                {
                    buffer.Clear();
                }
            }
            else
            {
                lock (_lock)
                {
                    buffer.RemoveRange(0, bufferIndexOf);
                }
                CacheAndConsume([], connectionId, buffer, consumer);
            }
        }

        lock (_lock)
        {
            bufferLength = buffer.Count;
        }

        // Arta kalanları veri için bu methodu yeniden çalıştır
        if (bufferLength >= minimumPacketLength) CacheAndConsume([], connectionId, buffer, consumer);

#if RELEASE
        }
        catch { }
#endif
    }

    public static int IndexOf<T>(this IEnumerable<T> source, IEnumerable<T> search)
    {
        var index = -1;
        for (var i = 0; i <= source.Count() - search.Count(); i++)
        {
            var matched = true;
            for (var j = 0; j < search.Count(); j++)
            {
                matched = matched && source.ElementAt(i + j).Equals(search.ElementAt(j));
            }
            if (matched)
            {
                index = i;
                break;
            }
        }
        return index;
    }
}
