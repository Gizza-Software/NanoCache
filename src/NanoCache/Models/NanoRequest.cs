using MessagePack;
using NanoCache.Enums;
using System;

namespace NanoCache.Models
{
    [MessagePackObject]
    public class NanoRequest
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
        public NanoCacheEntryOptions Options { get; set; }
    }

    [MessagePackObject]
    public class NanoCacheEntryOptions
    {
        [Key(1)]
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        [Key(2)]
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        [Key(3)]
        public TimeSpan? SlidingExpiration { get; set; }
    }

    internal class NanoWaitingRequest
    {
        public NanoClient Client { get; set; }
        public NanoRequest Request { get; set; }
        public long ConnectionId { get; set; }
    }

}
