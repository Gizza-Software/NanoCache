namespace NanoCache;

public enum SocketSecurity
{
    None = 0,
    CRC16 = 1,
    CRC32 = 2,
}

internal static class SocketHelpers
{
    public static void CacheAndConsumeEx(string connectionId, List<byte> buffer, byte[] data, Action<byte[], string> consumer)
    {
        try
        {
            buffer.AddRange(data);
            var buff = buffer.ToArray();

            // Minimum packet length: 8 bytes
            // * SYNC     : 2 Bytes
            // * Length   : 4 Bytes (int)
            // * Data Type: 1 Byte  (byte)
            // * Content  : 1 Byte(Minimum)
            // * CRC16    : 2 Bytes (ushort)
            // * CRC32    : 4 Bytes (uint)

            var crcLength = 0;
            var syncLength = NanoCacheConstants.PacketHeader.Length;
            var lengthLength = 4;
            var dataTypeLength = 1;
            var minimumDataLength = 1;
            if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16) crcLength = 2;
            else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32) crcLength = 4;
            var minimumPacketLength = syncLength + lengthLength + dataTypeLength + crcLength + minimumDataLength;

            if (buff.Length < minimumPacketLength)
                return;

            var indexOf = buff.IndexOf(NanoCacheConstants.PacketHeader);
            if (indexOf == -1)
            {
                // NOTE: !!! Clear causes memory leak !!!
                // buffer.Clear();
                // buffer.RemoveRange(0, buff.Length);
                buffer = [];
            }
            else if (indexOf == 0) // SYNC Bytes
            {
                // lenghtValue = Data Type (1) + Content (X)
                // lenghtValue doenst contains CRC bytes.
                var lenghtValue = BitConverter.ToInt32(buff, syncLength);

                // Is packet length enough?
                var packetLength = syncLength + lengthLength + lenghtValue + crcLength;
                if (buff.Length >= packetLength)
                {
                    // CRC-Body'i ayarlayalım
                    var preBytesLength = syncLength + lengthLength;
                    // var crcBody = new byte[lenghtValue];
                    // Array.Copy(buff, preBytesLength, crcBody, 0, lenghtValue);
                    var crcBody = buff.Skip(preBytesLength).Take(lenghtValue).ToArray();

                    // Check CRC & Consume
                    try
                    {
                        // Check Point
                        var consume = false;
                        if (NanoCacheConstants.SocketSecurity == SocketSecurity.None)
                        {
                            consume = true;
                        }
                        else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16)
                        {
                            // var crcBytes = new byte[crcLength];
                            // Array.Copy(buff, lenghtValue + preBytesLength, crcBytes, 0, crcLength);
                            var crcBytes = buff.Skip(lenghtValue + preBytesLength).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                            consume = CRC16.CheckChecksum(crcBody, crcValue);
                        }
                        else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32)
                        {
                            // var crcBytes = new byte[crcLength];
                            // Array.Copy(buff, lenghtValue + preBytesLength, crcBytes, 0, crcLength);
                            var crcBytes = buff.Skip(lenghtValue + preBytesLength).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt32(crcBytes, 0);
                            consume = CRC32.CheckChecksum(crcBody, crcValue);
                        }

                        // Consume
                        if (consume)
                        {
                            consumer(crcBody, connectionId);
                        }
                    }
                    catch { }

                    // Consume edilen veriyi buffer'dan at
                    buffer.RemoveRange(0, packetLength);

                    // Arta kalanları veri için bu methodu yeniden çalıştır
                    if (buffer.Count > 0)
                    {
                        CacheAndConsumeEx(connectionId, buffer, [], consumer);
                    }
                }
            }
            else
            {
                buffer.RemoveRange(0, indexOf);
                CacheAndConsumeEx(connectionId, buffer, [], consumer);
            }
        }
        catch { }
    }

    public static void CacheAndConsume(string connectionId, ref List<byte> buffer, byte[] data, Action<byte[], string> consumer)
    {
#if RELEASE
        try
        {
#endif
        var crcLength = 0;
        var syncLength = NanoCacheConstants.PacketHeader.Length;
        var lengthLength = 4;
        var dataTypeLength = 1;
        var minimumDataLength = 1;
        if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16) crcLength = 2;
        else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32) crcLength = 4;
        var minimumPacketLength = syncLength + lengthLength + dataTypeLength + crcLength + minimumDataLength;
        // Minimum packet length: 8 bytes
        // * SYNC     : 2 Bytes
        // * Length   : 4 Bytes (int)
        // * Data Type: 1 Byte  (byte)
        // * Content  : 1 Byte(Minimum)
        // * CRC16    : 2 Bytes (ushort)
        // * CRC32    : 4 Bytes (uint)

        buffer.AddRange(data);
        if (buffer.Count < minimumPacketLength)
            return;

        var indexOf = buffer.IndexOf(NanoCacheConstants.PacketHeader);
        if (indexOf == -1)
        {
            // NOTE: !!! Clear causes memory leak !!!
            // buffer.Clear();
            // buffer.RemoveRange(0, buff.Length);
            buffer = [];
        }
        else if (indexOf == 0)
        {
            // lenghtValue = Data Type (1) + Content (X)
            // lenghtValue doesnt contains CRC bytes.
            var lenghtBytes = buffer.Skip(syncLength).Take(4).ToArray();
            var lenghtValue = BitConverter.ToInt32(lenghtBytes, 0);
            var packetLength = syncLength + lengthLength + lenghtValue + crcLength;
            if (buffer.Count >= packetLength)
            {
#if RELEASE
        try
        {
#endif
                var consume = false;
                var preBytesLength = syncLength + lengthLength;
                var crcBody = buffer.Skip(preBytesLength).Take(lenghtValue).ToArray();

                if (NanoCacheConstants.SocketSecurity == SocketSecurity.None)
                {
                    consume = true;
                }
                else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16)
                {
                    var crcBytes = buffer.Skip(lenghtValue + preBytesLength).Take(crcLength).ToArray();
                    var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                    consume = CRC16.CheckChecksum(crcBody, crcValue);
                }
                else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32)
                {
                    var crcBytes = buffer.Skip(lenghtValue + preBytesLength).Take(crcLength).ToArray();
                    var crcValue = BitConverter.ToUInt32(crcBytes, 0);
                    consume = CRC32.CheckChecksum(crcBody, crcValue);
                }

                if (consume) consumer(crcBody, connectionId);
#if RELEASE
        }
        catch { }
#endif

                // Consume edilen veriyi buffer'dan at
                buffer.RemoveRange(0, packetLength);

                // Arta kalanları veri için bu methodu yeniden çalıştır
                if (buffer.Count > 0)
                {
                    CacheAndConsume(connectionId, ref buffer, [], consumer);
                }
            }
        }
        else
        {
            buffer.RemoveRange(0, indexOf);
            CacheAndConsume(connectionId, ref buffer, [], consumer);
        }
#if RELEASE
        }
        catch { }
#endif
    }

    public static int IndexOf<T>(this IEnumerable<T> source, IEnumerable<T> search)
    {
        var index = -1;
        var sourceLength = source.Count();
        var searchLength = search.Count();
        for (var i = 0; i <= sourceLength - searchLength; i++)
        {
            var matched = true;
            for (var j = 0; j < searchLength; j++)
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
