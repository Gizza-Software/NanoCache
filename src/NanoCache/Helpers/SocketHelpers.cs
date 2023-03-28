namespace NanoCache.Helpers;

public enum SocketSecurity
{
    None = 0,
    CRC16 = 1,
    CRC32 = 2,
}

internal static class SocketHelpers
{
    public static void CacheAndConsume(byte[] bytes, string connectionId, List<byte> buffer, Action<byte[], string> consumer)
    {
        var security = SocketSecurity.CRC32;
        var header = new byte[] { 0xF1, 0xF2 };

        try
        {
            // Gelen verileri buffer'a ekle ve bu halini "buff" olarak al. Sonrasında bufferı temizle
            buffer.AddRange(bytes);
            var buff = buffer.ToArray();

            // Minimum paket uzunluğu 8 byte
            // * SYNC     : 2 Bytes
            // * Length   : 2 Bytes
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

            if (buff.Length >= minimumPacketLength)
            {
                var indexOf = buff.IndexOf(header);
                if (indexOf == -1) buffer.Clear();
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
                            if (security == SocketSecurity.None)
                            {
                                consume = true;
                            }
                            else if (security == SocketSecurity.CRC16)
                            {
                                var crcBytes = new byte[crcLength];
                                Array.Copy(buff, lenghtValue + preBytesLength, crcBytes, 0, crcLength);
                                var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                                consume = CRC16.CheckChecksum(crcBody, crcValue);
                            }
                            else if (security == SocketSecurity.CRC32)
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
                        catch (Exception ex)
                        {
                            var a = ex;
                        }

                        // Consume edilen veriyi buffer'dan at
                        var bufferLength = buffer.Count;
                        buffer.RemoveRange(0, packetLength);

                        // Arta kalanları veri için bu methodu yeniden çalıştır
                        if (bufferLength > packetLength)
                        {
                            CacheAndConsume(Array.Empty<byte>(), connectionId, buffer, consumer);
                        }
                    }
                }
                else
                {
                    buffer.RemoveRange(0, indexOf);
                    CacheAndConsume(Array.Empty<byte>(), connectionId, buffer, consumer);
                }
            }
        }
        catch(Exception ex) 
        {
            var a = ex;
        }
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
