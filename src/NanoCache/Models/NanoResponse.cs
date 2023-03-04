namespace NanoCache.Models;

[MessagePackObject]
public class NanoResponse
{
    [Key(0)]
    public long Identifier { get; set; }

    [Key(1)]
    public NanoOperation Operation { get; set; }

    [Key(2)]
    public string Key { get; set; }

    [Key(3)]
    public byte[] Value { get; set; }

    [Key(4)]
    public bool Success { get; set; }
}
