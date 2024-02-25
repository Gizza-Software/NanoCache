namespace NanoCache;

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
        var security = SocketSecurity.None;
        var header = new List<byte> { 0xF1, 0xF2 };

#if RELEASE
        try
        {
#endif
        // * SYNC     : 2 Bytes
        // * Length   : 4 Bytes
        // * Data Type: 1 Byte
        // * Content  : 1 Byte(Minimum)
        // * CRC16    : 2 Bytes
        // * CRC32    : 4 Bytes
        var crcLength = 0;
        var syncLength = header.Count;
        var sizeLength = 4;
        var dataTypeLength = 1;
        var minimumDataLength = 1;
        var headerLength = syncLength + sizeLength;
        var minimumPackageLength = syncLength + sizeLength + dataTypeLength + crcLength + minimumDataLength;
        if (security == SocketSecurity.CRC16) crcLength = 2;
        else if (security == SocketSecurity.CRC32) crcLength = 4;

        buffer.AddRange(bytes);
        if(buffer.Count>1_024_000)
        {
            var a = 0;
        }
        while (buffer.Count > 0)
        {
            var consume = false;
            var packageLength = 0;
            var bufferLength = buffer.Count;
            var bufferCursor = buffer.IndexOfList(header);
            if (bufferLength >= minimumPackageLength)
            {
                if (bufferCursor == 0)
                {
                    // lenghtValue = Data Type (1) + Content (X)
                    var lenghtBytes = buffer.Skip(syncLength).Take(4).ToArray();
                    var lengthValue = BitConverter.ToInt32(lenghtBytes, 0);
                    packageLength = syncLength + sizeLength + lengthValue + crcLength;
                    if (bufferLength >= packageLength)
                    {
                        // Security
                        byte[] payload = null;
                        if (security == SocketSecurity.None)
                        {
                            payload = buffer.Skip(headerLength).Take(lengthValue).ToArray();
                            consume = true;
                        }
                        else if (security == SocketSecurity.CRC16)
                        {
                            var crcBytes = buffer.Skip(headerLength + lengthValue).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt16(crcBytes, 0);
                            payload = buffer.Skip(headerLength).Take(lengthValue).ToArray();
                            consume = CRC16.CheckChecksum(payload, crcValue);
                        }
                        else if (security == SocketSecurity.CRC32)
                        {
                            var crcBytes = buffer.Skip(lengthValue + headerLength).Take(crcLength).ToArray();
                            var crcValue = BitConverter.ToUInt32(crcBytes, 0);
                            payload = buffer.Skip(headerLength).Take(lengthValue).ToArray();
                            consume = CRC32.CheckChecksum(payload, crcValue);
                        }

                        // Remove from Buffer
                        buffer.RemoveRange(0, packageLength);

                        // Consume
                        // if (consume) Task.Run(() => consumer(payload, connectionId));
                        if (consume) consumer(payload, connectionId);
                    }
                }
                else if (bufferCursor == -1) buffer.Clear();
                else buffer.RemoveRange(0, bufferCursor);
            }

            // Check Point
            if (!consume || buffer.Count < packageLength)
                break;
        }

#if RELEASE
        } finally { }
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

    public static int IndexOfList<T>(this List<T> source, List<T> search)
    {
        var index = -1;
        if (source == null || search == null) return index;

        for (var i = 0; i <= source.Count - search.Count; i++)
        {
            var matched = true;
            for (var j = 0; j < search.Count; j++)
            {
                matched = matched && source[i + j].Equals(search[j]);
            }
            if (matched) return i;
        }

        return index;
    }
}
