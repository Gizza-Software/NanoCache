namespace NanoCache;

internal static class SocketExtensions
{
    public static byte[] PrepareObjectToSend(this NanoRequest @this)
        => BinaryHelpers.Serialize(@this).PrepareBytesToSend((byte)@this.Operation);

    public static byte[] PrepareObjectToSend(this NanoResponse @this)
        => BinaryHelpers.Serialize(@this).PrepareBytesToSend((byte)@this.Operation);

    public static byte[] PrepareBytesToSend(this byte[] @this, byte dataType)
    {
        // SYNC: 2 Bytes
        var list = new List<byte>();
        foreach (var sync in NanoCacheConstants.PacketHeader)
        {
            list.Add(sync);
        }

        // Length: 4 Bytes
        var len = @this.Length + 1; // +1, SocketResponseDataType için
        list.AddRange(len.ToByteList());

        // Data Type: 1 Byte
        list.Add(dataType);

        // Content
        list.AddRange([.. @this]);

        // CRC Body
        var crcBody = new List<byte>();
        crcBody.Add(dataType);
        crcBody.AddRange(@this);
        var crcBytes = crcBody.ToArray();

        // CRC16: 2 Bytes
        if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC16)
        {
            var crc16 = CRC32.ComputeChecksum(crcBytes);
            list.AddRange(crc16.ToByteList());
        }

        // CRC32: 4 Bytes
        if (NanoCacheConstants.SocketSecurity == SocketSecurity.CRC32)
        {
            var crc32 = CRC32.ComputeChecksum(crcBytes);
            list.AddRange(crc32.ToByteList());
        }

        // ToArray
        return [.. list];
    }

    public static byte[] ToBytes(this short @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this short @this) => [.. @this.ToBytes()];

    public static byte[] ToBytes(this int @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this int @this) => [.. @this.ToBytes()];

    public static byte[] ToBytes(this uint @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this uint @this) => [.. @this.ToBytes()];

    public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
    {
        foreach (var element in toAdd)
        {
            @this.Add(element);
        }
    }
}