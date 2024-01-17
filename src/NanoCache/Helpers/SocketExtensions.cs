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
        list.Add(0xF1);
        list.Add(0xF2);

        // Length: 4 Bytes
        var len = @this.Length + 1; // +1, SocketResponseDataType için
        list.AddRange(len.ToByteList());

        // Data Type: 1 Byte
        list.Add(dataType);

        // Content
        list.AddRange(@this.ToList());

        // CRC Body
        var crcBody = new List<byte>();
        crcBody.Add(dataType);
        crcBody.AddRange(@this);
        var crcBytes = crcBody.ToArray();

        /*
        // CRC16: 2 Bytes
        byte crc_01 = 0x00, crc_02 = 0x00;
        CRC16.ComputeCRC16(crcBytes, ref crc_01, ref crc_02);
        list.Add(crc_01);
        list.Add(crc_02);
        */

        // CRC32: 4 Bytes
        var crc32 = CRC32.ComputeChecksum(crcBytes);
        list.AddRange(crc32.ToByteList());

        // ToArray
        return list.ToArray();
    }

    public static byte[] ToBytes(this short @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this short @this) => @this.ToBytes().ToList();

    public static byte[] ToBytes(this int @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this int @this) => @this.ToBytes().ToList();

    public static byte[] ToBytes(this uint @this) => BitConverter.GetBytes(@this);
    public static List<byte> ToByteList(this uint @this) => @this.ToBytes().ToList();

    public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
    {
        foreach (var element in toAdd)
        {
            @this.Add(element);
        }
    }
}