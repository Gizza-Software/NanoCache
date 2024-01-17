namespace NanoCache;

public class NanoResponse
{
    public long Identifier { get; set; }
    public NanoOperation Operation { get; set; }
    public string Key { get; set; }
    public byte[] Value { get; set; }
    public bool Success { get; set; }
}
