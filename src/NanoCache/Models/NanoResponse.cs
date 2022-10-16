using MessagePack;
using NanoCache.Enums;

namespace NanoCache.Models
{
    [MessagePackObject]
    public class NanoResponse
    {
        [Key(1)]
        public long Identifier { get; set; }

        [Key(2)]
        public NanoOperation Operation { get; set; }

        [Key(3)]
        public string Key { get; set; }

        [Key(4)]
        public byte[] Value { get; set; }

        [Key(5)]
        public bool Success { get; set; }
    }
}
