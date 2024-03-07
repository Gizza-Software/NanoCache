namespace NanoCache;

public enum SocketSecurity
{
    None = 0,
    CRC16 = 1,
    CRC32 = 2,
}

internal static class SocketHelpers
{
    private static readonly object _lock = new object();

    public static void CacheAndConsume(string connectionId, ref List<byte> buffer, byte[] data, Action<byte[], string> consumer)
    {
        try
        {
            // Gelen verileri buffer'a ekle ve bu halini "buff" olarak al. Sonrasında bufferı temizle
            byte[] buff = null;
            lock (_lock)
            {
                buffer.AddRange(data);
                buff = [.. buffer];
            }

            // Minimum paket uzunluğu 8 byte
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
            var minimumPacketLength = syncLength + lengthLength + dataTypeLength + crcLength + minimumDataLength;
            if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16) crcLength = 2;
            else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32) crcLength = 4;

            if (buff.Length < minimumPacketLength)
                return;

            var indexOf = buff.IndexOf(NanoCacheConstants.PacketHeader);
            if (indexOf == -1)
            {
                lock (_lock)
                {
                    buffer.Clear();
                }
            }
            else if (indexOf == 0) // SYNC Bytes
            {
                // lenghtValue = Data Type (1) + Content (X)
                // lenghtValue CRC bytelarını kapsamıyor.
                var lenghtValue = BitConverter.ToInt32(buff, syncLength);

                // Paket yeterki kadar büyük mü? 
                // Paketin gereğinden fazla büyük olması sorun değil.
                var packetLength = syncLength + lengthLength + lenghtValue + crcLength;
                if (buff.Length >= packetLength)
                {
                    // CRC-Body'i ayarlayalım
                    var crcBody = new byte[lenghtValue];
                    var preBytesLength = syncLength + lengthLength;
                    Array.Copy(buff, preBytesLength, crcBody, 0, lenghtValue);

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
                            var crcBytes = new byte[crcLength];
                            Array.Copy(buff, lenghtValue + preBytesLength, crcBytes, 0, crcLength);
                            var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                            consume = CRC16.CheckChecksum(crcBody, crcValue);
                        }
                        else if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32)
                        {
                            var crcBytes = new byte[crcLength];
                            Array.Copy(buff, lenghtValue + preBytesLength, crcBytes, 0, crcLength);
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
                    var bufferLength = 0;
                    lock (_lock)
                    {
                        bufferLength = buffer.Count;
                        buffer.RemoveRange(0, packetLength);
                    }

                    // Arta kalanları veri için bu methodu yeniden çalıştır
                    if (bufferLength > packetLength)
                    {
                        CacheAndConsume(connectionId, ref buffer, [], consumer);
                    }
                }
            }
            else
            {
                lock (_lock)
                {
                    buffer.RemoveRange(0, indexOf);
                }
                CacheAndConsume(connectionId, ref buffer, [], consumer);
            }
        }
        catch { }
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
